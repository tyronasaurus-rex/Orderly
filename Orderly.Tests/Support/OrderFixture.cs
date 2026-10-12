using Orderly.Application;
using Orderly.Infrastructure;

namespace Orderly.Tests.Support;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
}

internal sealed class OrderFixture : IDisposable
{
    public OrderFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "orderly-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Repository = new JsonFileOrderRepository(Path.Combine(Root, "orders.json"));
        Clock = new FakeClock();
        SubmitOrder = new SubmitOrder(Repository, Clock);
        GetOrder = new GetOrder(Repository);
        ListOrders = new ListOrders(Repository);
        ChangeOrderStatus = new ChangeOrderStatus(Repository);
    }

    public string Root { get; }

    public string OrdersFile => Path.Combine(Root, "orders.json");

    public JsonFileOrderRepository Repository { get; }

    public FakeClock Clock { get; }

    public SubmitOrder SubmitOrder { get; }

    public GetOrder GetOrder { get; }

    public ListOrders ListOrders { get; }

    public ChangeOrderStatus ChangeOrderStatus { get; }

    public SubmitOrderCommand Command(
        string reference = "PO-1001",
        string customer = "Northwind",
        params SubmitOrderLine[] lines)
    {
        if (lines.Length == 0)
            lines = [new SubmitOrderLine("Binder", 2, 3.5m)];

        return new SubmitOrderCommand(reference, customer, lines);
    }

    public void Dispose()
    {
        Repository.Dispose();
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
