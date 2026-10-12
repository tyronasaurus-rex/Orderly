namespace Orderly.Application;

public sealed class ListOrders(IOrderRepository orders)
{
    public async Task<IReadOnlyList<OrderDetails>> HandleAsync(CancellationToken cancellationToken)
    {
        var stored = await orders.ListAsync(cancellationToken);
        return stored
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Select(OrderDetails.From)
            .ToArray();
    }
}
