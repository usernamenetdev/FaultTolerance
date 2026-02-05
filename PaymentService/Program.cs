using Graduation.ServiceDefaults.Metrics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using PaymentService.Contracts;
using PaymentService.Data;
using PaymentService.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddSqlServerDbContext<PaymentDbContext>(connectionName: "paymentdb");

builder.Services.AddScoped<PaymentApplicationService>();
builder.Services.AddResilienceMetrics();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger"; // /swagger
        options.SwaggerEndpoint("/openapi/v1.json", "NotificationService v1");
    });
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.Migrate();
}

app.MapDefaultEndpoints();

app.MapPost("/payments", async (
    HttpRequest http,
    CreatePaymentRequest req,
    PaymentApplicationService svc,
    CancellationToken ct) =>
{
    if (!http.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
        return Results.BadRequest(new { error = "MissingIdempotencyKey" });

    var keyGuid = Guid.TryParse(key.ToString(), out var parsedKey)
        ? parsedKey
        : Guid.Empty;

    if (keyGuid == Guid.Empty)
        return Results.BadRequest(new { error = "InvalidIdempotencyKey" });

    return await svc.CreatePaymentAsync(keyGuid, req, ct);
})
.AddOpenApiOperationTransformer((operation, context, ct) =>
{
    operation.Parameters ??= new List<IOpenApiParameter>();

    operation.Parameters.Add(new OpenApiParameter
    {
        Name = "Idempotency-Key",
        In = ParameterLocation.Header,
        Required = true,
        Description = "Ключ идемпотентности. Повтор с тем же ключом возвращает тот же результат. " +
                      "Повтор с другими параметрами — 409.",
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        }
    });

    return Task.CompletedTask;
});

app.MapGet("/payments/{id:guid}", async (Guid id, PaymentDbContext db, CancellationToken ct) =>
{
    var p = await db.Payments.AsNoTracking()
        .SingleOrDefaultAsync(x => x.OrderId == id, ct);

    if (p is null)
        return Results.NotFound(new { error = "PaymentNotFound" });

    return Results.Ok(new PaymentStatusResponse(
        PaymentId: p.Id,
        OrderId: p.OrderId,
        UserId: p.UserId,
        Amount: p.Amount,
        Currency: p.Currency,
        Fingerprint: p.Fingerprint,
        Status: p.Status.ToString(),
        FailureReason: p.FailureReason,
        CreatedAtUtc: p.CreatedAtUtc,
        UpdatedAtUtc: p.UpdatedAtUtc,
        CompletedAtUtc: p.CompletedAtUtc
    ));
})
.AddOpenApiOperationTransformer((op, ctx, ct) =>
{
    op.Summary = "Получить статус платежа";
    op.Description = "Используется для опроса статуса, если создание платежа вернуло 202 Accepted.";

    return Task.CompletedTask;
});

app.Run();
