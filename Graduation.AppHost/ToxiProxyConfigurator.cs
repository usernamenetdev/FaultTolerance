using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace Graduation.AppHost
{
    public sealed class ToxiProxyConfiguratorOptions
    {
        /// <summary>
        /// Если false — конфигурация toxiproxy не выполняется.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Адрес admin API toxiproxy (обычно проброшен на хост как localhost:8474).
        /// </summary>
        public Uri AdminBaseAddress { get; set; } = new("http://localhost:8474/");

        /// <summary>Порт, на котором toxiproxy слушает payment-proxy (listen).</summary>
        public int PaymentProxyListenPort { get; set; } = 9102;

        /// <summary>Порт, на котором toxiproxy слушает notification-proxy (listen).</summary>
        public int NotificationProxyListenPort { get; set; } = 9101;

        /// <summary>
        /// Хост для upstream, ВАЖНО:
        /// - если toxiproxy в контейнере, а сервисы на хосте: обычно "host.docker.internal" (Windows/Mac)
        /// - если toxiproxy тоже на хосте: "localhost"
        /// </summary>
        public string UpstreamHost { get; set; } = "host.docker.internal";

        /// <summary>
        /// Сбрасывать токсики и состояние прокси при старте (POST /reset).
        /// </summary>
        public bool ResetOnStart { get; set; } = true;

        /// <summary>
        /// Принудительно биндить проекты на 0.0.0.0:{port}, чтобы контейнер toxiproxy мог достучаться.
        /// </summary>
        public bool BindProjectsToAllInterfaces { get; set; } = true;

        /// <summary>Сколько раз ждать готовности admin API (/version).</summary>
        public int ReadyRetries { get; set; } = 60;

        /// <summary>Задержка между попытками готовности.</summary>
        public TimeSpan ReadyDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Если true — падать при ошибке конфигурации (обычно правильно для режима fault-injection).
        /// </summary>
        public bool FailFastOnError { get; set; } = true;
    }


    public static class ToxiProxyConfigurator
    {
        private const string HttpClientName = "toxiproxy-admin";

        public static void Configure(
            IDistributedApplicationBuilder builder,
            IResourceBuilder<ProjectResource> payment,
            IResourceBuilder<ProjectResource> notification,
            ToxiProxyConfiguratorOptions? options = null)
        {
            options ??= new();

            // Можно дополнительно завязать на USE_TOXIPROXY=true/false
            var useToxi = builder.Configuration["USE_TOXIPROXY"];
            if (!options.Enabled || (useToxi is not null && !IsTrue(useToxi)))
                return;

            var paymentHttp = payment.GetEndpoint("http");
            var notificationHttp = notification.GetEndpoint("http");

            if (options.BindProjectsToAllInterfaces)
            {
                payment.WithEnvironment(ctx =>
                    ctx.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://0.0.0.0:{paymentHttp.Port}");

                notification.WithEnvironment(ctx =>
                    ctx.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://0.0.0.0:{notificationHttp.Port}");
            }

            builder.Services.AddHttpClient(HttpClientName, c =>
            {
                c.BaseAddress = options.AdminBaseAddress;
            });

            // Событие может прийти несколько раз — нам нужно выполнить init один раз,
            // но только когда оба порта уже известны.
            int configured = 0;

            builder.Eventing.Subscribe<ResourceEndpointsAllocatedEvent>(async (@event, ct) =>
            {
                if (Volatile.Read(ref configured) == 1)
                    return;

                // Если один из портов ещё не выделен — ждём следующего события.
                if (paymentHttp.Port <= 0 || notificationHttp.Port <= 0)
                    return;

                if (Interlocked.Exchange(ref configured, 1) == 1)
                    return;

                var logger = @event.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ToxiProxyConfigurator");

                try
                {
                    var http = @event.Services.GetRequiredService<IHttpClientFactory>()
                        .CreateClient(HttpClientName);

                    await WaitForAdminAsync(http, options, logger, ct);

                    if (options.ResetOnStart)
                    {
                        var reset = await http.PostAsync("reset", content: null, ct);
                        reset.EnsureSuccessStatusCode();
                    }

                    // /populate принимает JSON-массив прокси; безопасно вызывать на старте много раз.
                    var payload = new[]
                    {
                    new ProxySpec(
                        name: "payment-proxy",
                        listen: $"0.0.0.0:{options.PaymentProxyListenPort}",
                        upstream: $"{options.UpstreamHost}:{paymentHttp.Port}",
                        enabled: true),

                    new ProxySpec(
                        name: "notification-proxy",
                        listen: $"0.0.0.0:{options.NotificationProxyListenPort}",
                        upstream: $"{options.UpstreamHost}:{notificationHttp.Port}",
                        enabled: true)
                };

                    var resp = await http.PostAsJsonAsync("populate", payload, ct);
                    resp.EnsureSuccessStatusCode();

                    logger.LogInformation(
                        "ToxiProxy populated: {PaymentListen} -> {PaymentUpstream}; {NotificationListen} -> {NotificationUpstream}",
                        payload[0].listen, payload[0].upstream,
                        payload[1].listen, payload[1].upstream);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to configure ToxiProxy.");

                    if (options.FailFastOnError)
                        throw;
                }
            });
        }

        private static async Task WaitForAdminAsync(
            HttpClient http,
            ToxiProxyConfiguratorOptions opt,
            ILogger logger,
            CancellationToken ct)
        {
            Exception? last = null;

            for (var i = 0; i < opt.ReadyRetries; i++)
            {
                try
                {
                    var r = await http.GetAsync("version", ct);
                    if (r.IsSuccessStatusCode)
                        return;

                    last = new HttpRequestException($"ToxiProxy admin returned {(int)r.StatusCode}.");
                }
                catch (Exception ex)
                {
                    last = ex;
                }

                await Task.Delay(opt.ReadyDelay, ct);
            }

            logger.LogError(last, "ToxiProxy admin API did not become ready.");
            throw new InvalidOperationException("ToxiProxy admin API did not become ready.", last);
        }

        private static bool IsTrue(string value) =>
            value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("on", StringComparison.OrdinalIgnoreCase);

        private sealed record ProxySpec(string name, string listen, string upstream, bool enabled);
    }
}
