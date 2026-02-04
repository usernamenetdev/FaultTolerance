namespace OrderService.Infrastructure
{
    public class PaymentClient
    {
        private readonly HttpClient _http;

        public PaymentClient(HttpClient http) => _http = http;
    }
}
