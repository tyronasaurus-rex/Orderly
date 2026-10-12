using Orderly.Domain;

namespace Orderly.Application;

public sealed record OrderLineDetails(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDetails(
    Guid Id,
    string ExternalReference,
    string CustomerName,
    IReadOnlyList<OrderLineDetails> Lines,
    decimal Total,
    string Status,
    DateTimeOffset CreatedAtUtc)
{
    public static OrderDetails From(Order order) =>
        new(
            order.Id,
            order.ExternalReference,
            order.CustomerName,
            order.Lines
                .Select(line => new OrderLineDetails(line.Description, line.Quantity, line.UnitPrice, line.LineTotal))
                .ToArray(),
            order.Total,
            order.Status.ToString(),
            order.CreatedAtUtc);
}
