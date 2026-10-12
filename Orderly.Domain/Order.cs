namespace Orderly.Domain;

public sealed class Order
{
    private Order(
        Guid id,
        string externalReference,
        string customerName,
        IReadOnlyList<OrderLine> lines,
        decimal total,
        OrderStatus status,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        ExternalReference = externalReference;
        CustomerName = customerName;
        Lines = lines;
        Total = total;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string ExternalReference { get; }

    public string CustomerName { get; }

    public IReadOnlyList<OrderLine> Lines { get; }

    public decimal Total { get; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Order Create(
        string? externalReference,
        string? customerName,
        IReadOnlyList<NewOrderLine>? lines,
        DateTimeOffset createdAtUtc)
    {
        var reference = RequireReference(externalReference);
        var customer = RequireText(customerName, "Customer name", OrderFieldLimits.CustomerNameMaxLength);
        var builtLines = RequireLines(lines);
        return new Order(
            Guid.NewGuid(),
            reference,
            customer,
            builtLines,
            TotalOf(builtLines),
            OrderStatus.Submitted,
            createdAtUtc);
    }

    public static Order Rehydrate(
        Guid id,
        string? externalReference,
        string? customerName,
        IReadOnlyList<NewOrderLine>? lines,
        OrderStatus status,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new InvalidDataException("Stored order id is missing.");

        if (!Enum.IsDefined(status))
            throw new InvalidDataException("Stored order status is not recognized.");

        var reference = RequireReference(externalReference);
        var customer = RequireText(customerName, "Customer name", OrderFieldLimits.CustomerNameMaxLength);
        var builtLines = RequireLines(lines);
        return new Order(id, reference, customer, builtLines, TotalOf(builtLines), status, createdAtUtc);
    }

    public bool MatchesContent(Order other)
    {
        if (!string.Equals(CustomerName, other.CustomerName, StringComparison.Ordinal))
            return false;

        if (Lines.Count != other.Lines.Count)
            return false;

        for (var index = 0; index < Lines.Count; index++)
        {
            if (!Lines[index].Matches(other.Lines[index]))
                return false;
        }

        return true;
    }

    public bool TryChangeStatus(OrderStatus next, out string rejectionMessage)
    {
        if (!OrderStatusPolicy.IsAllowed(Status, next))
        {
            rejectionMessage = OrderStatusPolicy.RejectionMessage(Id, Status);
            return false;
        }

        Status = next;
        rejectionMessage = "";
        return true;
    }

    private static decimal TotalOf(IReadOnlyList<OrderLine> lines) =>
        Money.Round(lines.Sum(line => line.LineTotal));

    private static string RequireReference(string? value)
    {
        var normalized = PurchaseOrderReference.Normalize(value);
        if (normalized.Length == 0)
            throw new OrderValidationException("External reference is required.");

        if (normalized.Length > OrderFieldLimits.ExternalReferenceMaxLength)
        {
            throw new OrderValidationException(
                $"External reference must be {OrderFieldLimits.ExternalReferenceMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string RequireText(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new OrderValidationException($"{name} is required.");

        if (trimmed.Length > maxLength)
            throw new OrderValidationException($"{name} must be {maxLength} characters or fewer.");

        return trimmed;
    }

    private static IReadOnlyList<OrderLine> RequireLines(IReadOnlyList<NewOrderLine>? lines)
    {
        if (lines is null || lines.Count == 0)
            throw new OrderValidationException("An order must include at least one line.");

        var built = new OrderLine[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            built[index] = OrderLine.Create(line.Description, line.Quantity, line.UnitPrice, index + 1);
        }

        return built;
    }
}
