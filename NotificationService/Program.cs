using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Встроенный OpenAPI документ (генерация JSON через MapOpenApi)
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// OpenAPI JSON + Swagger UI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger"; // /swagger
        options.SwaggerEndpoint("/openapi/v1.json", "NotificationService v1");
    });
}

var group = app.MapGroup("/api/notifications")
    .WithTags("Notifications");

// Единственная боёвая ручка: доставка уведомления (magic-link и receipt идут через Outbox)
var deliverEndpoint = group.MapPost("/deliver", (DeliverNotificationRequest req, ILogger<Program> log) =>
{
    // Логика сервиса: всегда OK (сбои/таймауты моделируются ToxiProxy, а не кодом сервиса)
    log.LogInformation(
        "Уведомление получено. type={Type}",
        req.Type);

    return Results.Ok(new
    {
        ok = true,
        receivedAtUtc = DateTime.UtcNow,
        type = req.Type
    });
})
.WithName("DeliverNotification");


deliverEndpoint.AddOpenApiOperationTransformer((operation, context, ct) =>
{
    operation.Summary = "Приём уведомления из Outbox";
    operation.Description =
        "NotificationService является заглушкой отправки уведомлений и всегда возвращает 200 OK. " +
        "Негативные сценарии (задержки/таймауты/разрывы соединения) моделируются средствами ToxiProxy.";

    return Task.CompletedTask;
});

// Опциональный ping для ручной проверки
group.MapGet("/ping", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }))
     .WithName("Ping");

app.Run();

public sealed record DeliverNotificationRequest(
    string Type
);
