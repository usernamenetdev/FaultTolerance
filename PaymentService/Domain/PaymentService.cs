using Graduation.ServiceDefaults.Metrics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentService.Contracts;
using PaymentService.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PaymentService.Domain
{
    public sealed class PaymentApplicationService
    {
        private readonly PaymentDbContext _db;
        private readonly IResilienceMetrics _metrics;
        private readonly CancellationToken _appStopping;

        public PaymentApplicationService(PaymentDbContext db, IResilienceMetrics metrics, IHostApplicationLifetime lifetime)
        {
            _db = db;
            _metrics = metrics;
            _appStopping = lifetime.ApplicationStopping;
        }

        public async Task<IResult> CreatePaymentAsync(
            Guid idempotencyKey,
            CreatePaymentRequest req,
            CancellationToken ct)
        {

            using var dbCts = CancellationTokenSource.CreateLinkedTokenSource(_appStopping);
            dbCts.CancelAfter(TimeSpan.FromSeconds(2));  // внутренний дедлайн БД
            var dbCt = dbCts.Token;

            var now = DateTime.UtcNow;

            req = req with
            {
                Currency = req.Currency.Trim().ToUpperInvariant(),
                Fingerprint = req.Fingerprint.Trim()
            };

            if (req.Currency.Length != 3)
            {
                return Results.BadRequest(new { error = "Валюта должна содержать 3-ёхсимвольный ISO код (например, RUB)" });
            }

            var requestHash = ComputeRequestHash(req);

            // === Фаза 1: захват ключа идемпотентности ===
            var paymentId = Guid.NewGuid();

            var idem = new PaymentIdempotency
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                PaymentId = paymentId,
                Status = IdempotencyStatus.InProgress,
                CreatedAtUtc = now
            };

            _db.PaymentIdempotency.Add(idem);

            try
            {
                await SaveChangesCriticalAsync(dbCt);

                // ✅ idempotency MISS: ключ новый, обработка будет выполняться
                _metrics.RecordIdempotencyResult("payment_create", IdempotencyResult.Miss);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _db.ChangeTracker.Clear();

                var existing = await _db.PaymentIdempotency
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

                if (existing is null)
                    throw;

                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    // ✅ idempotency CONFLICT: тот же ключ, но другие параметры
                    _metrics.RecordIdempotencyResult("payment_create", IdempotencyResult.Conflict);

                    return Results.Conflict(new
                    {
                        error = "IdempotencyKeyReuseWithDifferentParameters",
                        message = "Этот Idempotency-Key уже использован с другими параметрами запроса."
                    });
                }

                if (existing.Status == IdempotencyStatus.Completed)
                {
                    // ✅ idempotency HIT: результат уже зафиксирован
                    _metrics.RecordIdempotencyResult("payment_create", IdempotencyResult.Hit);

                    return Results.Ok(new
                    {
                        paymentId = existing.PaymentId,
                        status = existing.ResultStatus.ToString(),
                        error = existing.ResultError
                    });
                }

                // ✅ idempotency IN_PROGRESS: уже выполняется (возвращаем 202)
                _metrics.RecordIdempotencyResult("payment_create", IdempotencyResult.InProgress);

                return Results.Accepted($"/payments/{existing.PaymentId}", new
                {
                    paymentId = existing.PaymentId,
                    status = PaymentStatus.Pending.ToString()
                });
            }

            // === Фаза 2: ключ наш -> создаём Payment и завершаем обработку ===

            var payment = new Payment
            {
                Id = paymentId,
                OrderId = req.OrderId,
                UserId = req.UserId,
                Amount = req.Amount,
                Currency = req.Currency,
                Fingerprint = req.Fingerprint,

                Status = PaymentStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.Payments.Add(payment);

            try
            {
                await SaveChangesCriticalAsync(dbCt);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "OrderAlreadyPaid";
                payment.CompletedAtUtc = DateTime.UtcNow;
                payment.UpdatedAtUtc = payment.CompletedAtUtc.Value;

                idem.Status = IdempotencyStatus.Completed;
                idem.ResultStatus = PaymentStatus.Failed;
                idem.ResultError = "OrderAlreadyPaid";

                _db.Entry(payment).State = EntityState.Detached;

                await SaveChangesCriticalAsync(dbCt);

                return Results.Conflict(new
                {
                    error = "OrderAlreadyPaid",
                    message = "Для данного заказа уже существует платёж."
                });
            }

            try
            {
                await Task.Delay(50, ct);

                payment.Status = PaymentStatus.Completed;
                payment.CompletedAtUtc = DateTime.UtcNow;
                payment.UpdatedAtUtc = payment.CompletedAtUtc.Value;

                idem.Status = IdempotencyStatus.Completed;
                idem.ResultStatus = PaymentStatus.Completed;
                idem.ResultError = null;
            }
            catch (OperationCanceledException)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Canceled";
                payment.CompletedAtUtc = DateTime.UtcNow;
                payment.UpdatedAtUtc = payment.CompletedAtUtc.Value;

                idem.Status = IdempotencyStatus.Completed;
                idem.ResultStatus = PaymentStatus.Failed;
                idem.ResultError = "Canceled";
            }
            catch (Exception e)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "ProcessingError";
                payment.CompletedAtUtc = DateTime.UtcNow;
                payment.UpdatedAtUtc = payment.CompletedAtUtc.Value;

                idem.Status = IdempotencyStatus.Completed;
                idem.ResultStatus = PaymentStatus.Failed;
                idem.ResultError = e.GetType().Name;
            }

            await SaveChangesCriticalAsync(dbCt);

            return Results.Ok(new
            {
                paymentId = payment.Id,
                status = payment.Status.ToString(),
                error = payment.FailureReason
            });
        }

        private static string ComputeRequestHash(CreatePaymentRequest req)
        {
            var s = string.Join('|',
                req.OrderId.ToString("N"),
                req.UserId,
                req.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                req.Currency.Trim().ToUpperInvariant(),
                req.Fingerprint.Trim());

            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is SqlException sql && (sql.Number == 2627 || sql.Number == 2601);

        private async Task SaveChangesCriticalAsync(CancellationToken requestCt)
        {
            try
            {
                // Если клиент ещё жив — ок, сохраняем с request ct
                await _db.SaveChangesAsync(requestCt);
            }
            catch (OperationCanceledException) when (requestCt.IsCancellationRequested && !_appStopping.IsCancellationRequested)
            {
                // Клиент/прокси отменил запрос, но сервис ещё жив.
                // Сохранение финального состояния в БД.
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_appStopping);
                cts.CancelAfter(TimeSpan.FromSeconds(2)); // коротко, чтобы не залипнуть

                await _db.SaveChangesAsync(cts.Token);
            }
        }
    }
}