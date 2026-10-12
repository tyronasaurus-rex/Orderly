namespace Orderly.Domain;

public sealed class OrderLine
{
    private OrderLine(string description, int quantity, decimal unitPrice, decimal lineTotal)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
    }

    public string Description { get; }

    public int Quantity { get; }

    public decimal UnitPrice { get; }

    public decimal LineTotal { get; }

    public static OrderLine Create(string? description, decimal quantity, decimal unitPrice, int lineNumber)
    {
        var trimmedDescription = description?.Trim() ?? "";
        if (trimmedDescription.Length == 0)
            throw new OrderValidationException($"Line {lineNumber} description is required.");

        if (trimmedDescription.Length > OrderFieldLimits.DescriptionMaxLength)
        {
            throw new OrderValidationException(
                $"Line {lineNumber} description must be {OrderFieldLimits.DescriptionMaxLength} characters or fewer.");
        }

        if (quantity < 1 || quantity > int.MaxValue || quantity != decimal.Truncate(quantity))
            throw new OrderValidationException($"Line {lineNumber} quantity must be a positive whole number.");

        if (unitPrice < 0)
            throw new OrderValidationException($"Line {lineNumber} unit price must be zero or greater.");

        var wholeQuantity = (int)quantity;
        decimal lineTotal;
        try
        {
            lineTotal = Money.Round(wholeQuantity * unitPrice);
        }
        catch (OverflowException)
        {
            throw new OrderValidationException($"Line {lineNumber} unit price is too large.");
        }

        return new OrderLine(trimmedDescription, wholeQuantity, unitPrice, lineTotal);
    }

    public bool Matches(OrderLine other) =>
        string.Equals(Description, other.Description, StringComparison.Ordinal)
        && Quantity == other.Quantity
        && Money.Round(UnitPrice) == Money.Round(other.UnitPrice);
}
