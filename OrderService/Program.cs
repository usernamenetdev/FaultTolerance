using Graduation.ServiceDefaults.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi;
using OrderService;
using OrderService.Application;
using OrderService.Contracts;
using OrderService.Data;
using OrderService.Domain;
using OrderService.Infrastructure;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddSqlServerDbContext<OrderDbContext>(connectionName: "orderdb");

builder.Services.Configure<OutboxDispatcherOptions>(builder.Configuration.GetSection("Outbox"));

builder.Services.AddScoped<OutboxService>();
builder.Services.AddScoped<OrderApplicationService>();
builder.Services.AddResilienceMetrics();

builder.Services.AddHttpClient<PaymentClient>("PaymentClient", client =>
{
    client.BaseAddress = new(builder.Configuration["Services:PaymentBaseUrl"]
                                 ?? "http://paymentservice");
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

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
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

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

builder.Services.AddHostedService<OutboxDispatcherHostedService>();

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

app.MapPost("/orders", async Task<IResult> (
    HttpContext http,
    OrderApplicationService orderService,
    CancellationToken ct) =>
{
    var userId = http.Request.Headers["X-User-Id"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { error = "Отсутствует X-User-Id" });

    // Вызываем бизнес-логику создания заказа
    var order = await orderService.CreateOrderAsync(userId, 15, "RUB", Helpers.RandomString(), ct);

    return Results.Created("/", order);
})
.AddOpenApiOperationTransformer((operation, ctx, ct) => {
    // Описание параметра Idempotency-Key для OpenAPI (аналогично PaymentService)
    operation.Parameters ??= new List<IOpenApiParameter>();
    operation.Parameters.Add(new OpenApiParameter
    {
        Name = "X-User-Id",
        In = ParameterLocation.Header,
        Required = true,
        Description = "Идентификатор пользователя",
        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
    });
    return Task.CompletedTask;
});


app.MapPost("/magic-link", async Task<IResult> (
    HttpContext http,
    OutboxService svc,
    ResilienceMetrics metrics,
    CancellationToken ct) =>
{
    var userId = http.Request.Headers["X-User-Id"].ToString();
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { error = "Отсутствует X-User-Id" });

    try
    {
        await svc.CreateOutboxMessageAsync(OutboxType.Magiclink, userId, ct);
        return Results.Ok(new { status = "sent" });

    }
    catch (TaskCanceledException)
    {
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
})
.AddOpenApiOperationTransformer((operation, ctx, ct) => {
    operation.Parameters ??= new List<IOpenApiParameter>();

    operation.Parameters.Add(new OpenApiParameter
    {
        Name = "X-User-Id",
        In = ParameterLocation.Header,
        Required = true,
        Description = "Идентификатор пользователя",
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        }
    });
    return Task.CompletedTask;
});

app.Run();