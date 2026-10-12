namespace Orderly.Application;

public static class OrderText
{
    public static string NotFound(Guid orderId) => $"Order {orderId:D} was not found.";
}
