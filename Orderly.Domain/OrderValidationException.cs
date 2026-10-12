namespace Orderly.Domain;

public sealed class OrderValidationException : Exception
{
    public OrderValidationException(string message)
        : base(message)
    {
    }
}
