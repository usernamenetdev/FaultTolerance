namespace OrderService.Domain
{
    public enum OrderStatus : byte
    {
        Unknown = 0,    // не удалось отправить в PaymentService
        Pending = 1,    // ожидает завершения оплаты
        Completed = 2,  // оплата успешна, заказ подтверждён
        Failed = 3      // оплата не выполнена (отклонена или ошибка)
    }
}
