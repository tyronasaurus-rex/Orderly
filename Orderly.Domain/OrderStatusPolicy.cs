namespace Orderly.Domain;

public static class OrderStatusPolicy
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedTransitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Submitted] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Fulfilled, OrderStatus.Cancelled],
            [OrderStatus.Fulfilled] = [],
            [OrderStatus.Cancelled] = [],
        };

    public static bool TryParse(string? value, out OrderStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var name in Enum.GetNames<OrderStatus>())
        {
            if (!name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            status = Enum.Parse<OrderStatus>(name);
            return true;
        }

        return false;
    }

    public static bool IsAllowed(OrderStatus current, OrderStatus next)
    {
        var allowed = AllowedFor(current);
        for (var index = 0; index < allowed.Count; index++)
        {
            if (allowed[index] == next)
                return true;
        }

        return false;
    }

    public static string RejectionMessage(Guid orderId, OrderStatus current)
    {
        var allowed = AllowedFor(current);
        if (allowed.Count == 0)
            return $"Order {orderId:D} is {current}. No further status changes are allowed.";

        return $"Order {orderId:D} is {current}. You can change it to {FormatList(allowed)}.";
    }

    public static string UnknownStatusMessage() =>
        $"Status must be {FormatList(Enum.GetNames<OrderStatus>())}.";

    private static IReadOnlyList<OrderStatus> AllowedFor(OrderStatus current) =>
        AllowedTransitions.TryGetValue(current, out var allowed) ? allowed : [];

    private static string FormatList(IReadOnlyList<OrderStatus> statuses) =>
        FormatList(statuses.Select(status => status.ToString()).ToArray());

    private static string FormatList(IReadOnlyList<string> names)
    {
        if (names.Count == 1)
            return names[0];

        if (names.Count == 2)
            return $"{names[0]} or {names[1]}";

        return string.Join(", ", names.Take(names.Count - 1)) + ", or " + names[^1];
    }
}
