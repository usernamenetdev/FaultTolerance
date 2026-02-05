using Graduation.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sqlPassword", "StrongPassword!123", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword, port: 56801)
    .WithDataVolume("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var orderDb = sql.AddDatabase("orderdb");
var paymentDb = sql.AddDatabase("paymentdb");

var useToxiproxy = builder.Configuration.GetValue("USE_TOXIPROXY", false);

var toxiproxy = useToxiproxy
    ? builder.AddContainer("toxiproxy", "ghcr.io/shopify/toxiproxy")
        .WithHttpEndpoint(port: 8474, targetPort: 8474, name: "admin")
        .WithEndpoint(port: 9101, targetPort: 9101, name: "notification-proxy")
        .WithEndpoint(port: 9102, targetPort: 9102, name: "payment-proxy")
    : null;

var payment = builder.AddProject<Projects.PaymentService>("paymentservice")
    .WithReference(paymentDb)
    .WaitFor(paymentDb)
    .WithUrlForEndpoint("http", ep => new() { Url = "/swagger", DisplayText = "Swagger" });

var notification = builder.AddProject<Projects.NotificationService>("notificationservice")
    .WithUrlForEndpoint("http", ep => new() { Url = "/swagger", DisplayText = "Swagger" });

var order = builder.AddProject<Projects.OrderService>("orderservice")
    .WithReference(orderDb)
    .WaitFor(orderDb)
    .WithUrlForEndpoint("http", ep => new() { Url = "/swagger", DisplayText = "Swagger" });

if (useToxiproxy)
{
    notification.
        WithEndpoint("http", e => e.IsProxied = false);

    payment
        .WithEndpoint("http", e => e.IsProxied = false);

    order
        .WaitFor(toxiproxy!)
        .WithEnvironment("Services__NotificationBaseUrl", "http://localhost:9101")
        .WithEnvironment("Services__PaymentBaseUrl", "http://localhost:9102");

    toxiproxy!
        .WaitFor(notification)
        .WaitFor(payment);

    ToxiProxyConfigurator.Configure(
    builder,
    payment,
    notification,
    new ToxiProxyConfiguratorOptions
    {
        // если toxiproxy у теб€ контейнер, а сервисы на хосте Ч оставл€й host.docker.internal
        // если toxiproxy тоже на хосте Ч поставь "localhost"
        UpstreamHost = "host.docker.internal",

        PaymentProxyListenPort = 9102,
        NotificationProxyListenPort = 9101,
        AdminBaseAddress = new Uri("http://localhost:8474/"),

        // рекомендую, чтобы при каждом запуске токсики были выключены
        ResetOnStart = true
    });
}
else
{
    order
        .WaitFor(notification)
        .WaitFor(payment)
        .WithReference(notification)
        .WithReference(payment);
}

builder.AddProject<Projects.ApiGateway>("apigateway")
    .WaitFor(order);

var paymentPort = payment.GetEndpoint("http").EndpointAnnotation.Port;
var notificationPort = notification.GetEndpoint("http").EndpointAnnotation.Port;

builder.Build().Run();