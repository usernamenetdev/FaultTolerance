using Graduation.ServiceDefaults.Metrics;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi;
using OrderService.Infrastructure;
using Polly;
using Polly.CircuitBreaker;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddSqlServerDbContext<OrderDbContext>(connectionName: "orderdb");

builder.Services.AddScoped<OrderApplicationService>();
builder.Services.AddResilienceMetrics();

builder.Services.AddHttpClient<PaymentClient>("PaymentClient", client =>
{
    client.BaseAddress = new(builder.Configuration["Services:PaymentBaseUrl"]
                                 ?? "http://paymentservice");
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;

    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<NotificationClient>("NotificationClient", client =>
{
    client.BaseAddress = new(builder.Configuration["Services:NotificationBaseUrl"]
                                 ?? "http://notificationservice");
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;

    options.Retry.DisableForUnsafeHttpMethods();

    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
});


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
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.Migrate();
}

app.MapPost("/orders", async (
    HttpRequest http,
    CreateOrderRequest request,
    OrderApplicationService orderService,
    CancellationToken ct) =>
{
    // Проверяем наличие заголовка идемпотентности
    if (!http.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
        return Results.BadRequest(new { error = "MissingIdempotencyKey" });
    if (!Guid.TryParse(key.ToString(), out var idempotencyKey))
        return Results.BadRequest(new { error = "InvalidIdempotencyKey" });

    // Вызываем бизнес-логику создания заказа
    return await orderService.CreateOrderAsync(idempotencyKey, request, ct);
})
.AddOpenApiOperationTransformer((operation, ctx) => {
    // Описание параметра Idempotency-Key для OpenAPI (аналогично PaymentService)
    operation.Parameters ??= new List<IOpenApiParameter>();
    operation.Parameters.Add(new OpenApiParameter
    {
        Name = "Idempotency-Key",
        In = ParameterLocation.Header,
        Required = true,
        Description = "Ключ идемпотентности. Повторный запрос с тем же ключом не дублирует заказ.",
        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
    });
    return Task.CompletedTask;
});

app.MapPost("/magic-link", async Task<IResult> (
    HttpContext http,
    NotificationClient notification,
    ResilienceMetrics metrics,
    CancellationToken ct) =>
{
    var userId = http.Request.Headers["X-User-Id"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { error = "Отсутствует X-User-Id" });

    try
    {
        await notification.SendMagicLinkAsync(userId, ct);
        return Results.Ok(new { status = "sent" });
    }
    catch (BrokenCircuitException)
    {
        metrics.CircuitBreakerShortCircuit("notificationservice");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (TaskCanceledException)
    {
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
});

app.Run();