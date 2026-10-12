namespace Orderly.Application;

public sealed class GetOrder(IOrderRepository orders)
{
    public async Task<OrderDetails?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(id, cancellationToken);
        return order is null ? null : OrderDetails.From(order);
    }
}
