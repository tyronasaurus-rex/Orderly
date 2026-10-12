namespace Orderly.Domain;

public static class Money
{
    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
