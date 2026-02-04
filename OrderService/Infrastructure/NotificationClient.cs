using OrderService.Contracts;
using System.Net.Http.Json;
using System.Text;

namespace OrderService.Infrastructure;

public sealed class NotificationClient
{
    private readonly HttpClient _http;

    public NotificationClient(HttpClient http) => _http = http;

    public async Task SendMagicLinkAsync(string userId, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/deliver")
        {
            Content = JsonContent.Create(new DeliverRequest("magic-link"))
        };

        msg.Headers.TryAddWithoutValidation("X-User-Id", userId);

        using var resp = await _http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task SendReceiptAsync(string userId, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/deliver")
        {
            Content = JsonContent.Create(new DeliverRequest("receipt"))
        };

        msg.Headers.TryAddWithoutValidation("X-User-Id", userId);

        using var resp = await _http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();
    }
}
