namespace Orderly.Models;

public sealed class OrderResponse
{
    public required Guid Id { get; init; }

    public required string ExternalReference { get; init; }

    public required string CustomerName { get; init; }

    public required IReadOnlyList<OrderLineResponse> Lines { get; init; }

    public required decimal Total { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class OrderLineResponse
{
    public required string Description { get; init; }

    public required int Quantity { get; init; }

    public required decimal UnitPrice { get; init; }

    public required decimal LineTotal { get; init; }
}
