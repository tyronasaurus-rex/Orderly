using Orderly.Domain;

namespace Orderly.Application;

public sealed class ChangeOrderStatus(IOrderRepository orders)
{
    public async Task<ChangeOrderStatusResult> HandleAsync(
        Guid orderId,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!OrderStatusPolicy.TryParse(status, out var next))
            return new ChangeOrderStatusResult.Invalid(OrderStatusPolicy.UnknownStatusMessage());

        return await orders.UpdateAsync(collection =>
        {
            var order = collection.FindById(orderId);
            if (order is null)
            {
                return (ChangeOrderStatusResult)new ChangeOrderStatusResult.NotFound(
                    orderId,
                    OrderText.NotFound(orderId));
            }

            if (!order.TryChangeStatus(next, out var rejection))
                return new ChangeOrderStatusResult.TransitionRejected(orderId, rejection);

            collection.MarkUpdated();
            return new ChangeOrderStatusResult.Updated(OrderDetails.From(order));
        }, cancellationToken);
    }
}
