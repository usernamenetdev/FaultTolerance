using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults (health/otel/logging etc.)
builder.AddServiceDefaults();

// Rate + Burst limiter (Token Bucket) + 429 при перегрузке
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global limiter: не лимитируем инфраструктурные пути (health endpoints),
    // лимитируем всё остальное.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (InfraPaths.IsInfra(ctx.Request.Path))
        {
            return RateLimitPartition.GetNoLimiter("infra");
        }

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: "global",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 200,
                TokensPerPeriod = 100,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });

    // Per-user limiter: ключ = GUID из X-User-Id (в Items кладётся валидатором)
    options.AddPolicy("per-user", ctx =>
    {
        if (!ctx.Items.TryGetValue(UserContext.ItemKey, out var obj) || obj is not Guid userId)
        {
            // На случай некорректного пайплайна — фактически блокируем.
            return RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: "invalid-user",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromHours(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
        }

        var key = userId.ToString("N");

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: key,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 40,
                TokensPerPeriod = 20,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });
});

// YARP reverse proxy (Routes/Clusters из appsettings.json)
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Aspire-style health endpoints (/health, /alive, /ready)
app.MapDefaultEndpoints();

// 1) Валидация X-User-Id (GUID) — только для НЕ-инфраструктурных путей.
// Сохраняем GUID в HttpContext.Items, чтобы лимитер не парсил второй раз.
app.Use(async (ctx, next) =>
{
    if (InfraPaths.IsInfra(ctx.Request.Path))
    {
        await next();
        return;
    }

    if (!UserContext.TryReadUserId(ctx, out var userId))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "Заголовок X-User-Id обязателен и должен быть валидным GUID"
        });
        return;
    }

    ctx.Items[UserContext.ItemKey] = userId;
    await next();
});

// 2) Лимитер ДО проксирования
app.UseRateLimiter();

// 3) Проксирование + per-user policy
app.MapReverseProxy()
   .RequireRateLimiting("per-user");

app.Run();