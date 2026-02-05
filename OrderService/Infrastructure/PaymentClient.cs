using OrderService.Contracts;

namespace OrderService.Infrastructure
{
    public class PaymentClient
    {
        private readonly HttpClient _http;
        public PaymentClient(HttpClient http) => _http = http;

        public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest req, CancellationToken ct)
        {

            var request = new HttpRequestMessage(HttpMethod.Post, "payments")
            {
                Content = JsonContent.Create(req)
            };

            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                // Successful HTTP (200 OK)
                return await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken: ct)
                       ?? new PaymentResponse { Status = "Failed", Error = "Неизвестный ответ" };
            }
            // Не-200 (например 400/409/500): записываем в ошибку
            var error = await response.Content.ReadAsStringAsync(ct);
            return new PaymentResponse { Status = "Failed", Error = error };
        }
    }
}
