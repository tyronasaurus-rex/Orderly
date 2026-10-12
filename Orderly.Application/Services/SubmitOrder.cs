using Orderly.Domain;

namespace Orderly.Application;

public sealed class SubmitOrder(IOrderRepository orders, IClock clock)
{
    public const string ReferenceInUseDetail = "The external reference is already used.";

    public async Task<SubmitOrderResult> HandleAsync(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Order draft;
        try
        {
            draft = Order.Create(
                command.ExternalReference,
                command.CustomerName,
                command.Lines?.Select(line => new NewOrderLine(line.Description, line.Quantity, line.UnitPrice)).ToArray(),
                clock.UtcNow);
        }
        catch (OrderValidationException exception)
        {
            return new SubmitOrderResult.Invalid(exception.Message);
        }

        return await orders.UpdateAsync(collection =>
        {
            var existing = collection.FindByExternalReference(draft.ExternalReference);
            if (existing is null)
            {
                collection.Add(draft);
                return (SubmitOrderResult)new SubmitOrderResult.Created(OrderDetails.From(draft));
            }

            if (existing.MatchesContent(draft))
                return new SubmitOrderResult.Existing(OrderDetails.From(existing));

            return new SubmitOrderResult.ReferenceInUse(existing.Id, ReferenceInUseDetail);
        }, cancellationToken);
    }
}
