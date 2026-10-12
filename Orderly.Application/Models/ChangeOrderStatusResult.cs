namespace Orderly.Application;

public abstract record ChangeOrderStatusResult
{
    private ChangeOrderStatusResult()
    {
    }

    public sealed record Updated(OrderDetails Order) : ChangeOrderStatusResult;

    public sealed record NotFound(Guid OrderId, string Detail) : ChangeOrderStatusResult;

    public sealed record TransitionRejected(Guid OrderId, string Detail) : ChangeOrderStatusResult;

    public sealed record Invalid(string Detail) : ChangeOrderStatusResult;
}
