using System.Security.Cryptography;
using System.Text;
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
            return RateLimitPartition.GetNoLimiter("infra");

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: "global",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 15,
                TokensPerPeriod = 15,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });

    // Per-user limiter: ключ = GUID из X-User-Id (в Items кладётся валидатором)
    options.AddPolicy("per-user", ctx =>
    {
        if (InfraPaths.IsInfra(ctx.Request.Path))
            return RateLimitPartition.GetNoLimiter("infra");

        var raw = ctx.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
            raw = "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: raw,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 5,
                TokensPerPeriod = 5,
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