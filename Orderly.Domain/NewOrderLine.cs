namespace Orderly.Domain;

public readonly record struct NewOrderLine(string? Description, decimal Quantity, decimal UnitPrice);
