namespace Orderly.Application;

public abstract record SubmitOrderResult
{
    private SubmitOrderResult()
    {
    }

    public sealed record Created(OrderDetails Order) : SubmitOrderResult;

    public sealed record Existing(OrderDetails Order) : SubmitOrderResult;

    public sealed record ReferenceInUse(Guid OrderId, string Detail) : SubmitOrderResult;

    public sealed record Invalid(string Detail) : SubmitOrderResult;
}
