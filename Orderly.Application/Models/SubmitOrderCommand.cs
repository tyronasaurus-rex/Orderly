namespace Orderly.Application;

public sealed record SubmitOrderLine(string? Description, decimal Quantity, decimal UnitPrice);

public sealed record SubmitOrderCommand(
    string? ExternalReference,
    string? CustomerName,
    IReadOnlyList<SubmitOrderLine>? Lines);
