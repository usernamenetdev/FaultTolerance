using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

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

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimiter();

app.MapReverseProxy()
    .RequireRateLimiting("per-user");

app.Run();

