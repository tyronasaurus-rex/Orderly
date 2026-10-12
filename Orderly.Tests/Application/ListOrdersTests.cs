using Orderly.Application;
using Orderly.Domain;
using Orderly.Tests.Support;

namespace Orderly.Tests.Application;

public class ListOrdersTests
{
    [Fact]
    public async Task Newest_created_time_is_first_even_when_the_file_is_oldest_first()
    {
        using var fixture = new OrderFixture();
        fixture.Clock.UtcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var older = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-OLD"), CancellationToken.None));

        fixture.Clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-NEW"), CancellationToken.None));

        var listed = await fixture.ListOrders.HandleAsync(CancellationToken.None);

        Assert.Equal([newer.Order.Id, older.Order.Id], listed.Select(order => order.Id));
    }

    [Fact]
    public async Task Equal_timestamps_list_the_higher_id_first()
    {
        using var fixture = new OrderFixture();
        var stamp = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        var lower = Order.Rehydrate(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "PO-A",
            "Alpha",
            [new NewOrderLine("Pen", 1, 1m)],
            OrderStatus.Submitted,
            stamp);
        var higher = Order.Rehydrate(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "PO-B",
            "Beta",
            [new NewOrderLine("Pen", 1, 1m)],
            OrderStatus.Submitted,
            stamp);

        await fixture.Repository.UpdateAsync(collection =>
        {
            collection.Add(lower);
            collection.Add(higher);
            return 0;
        }, CancellationToken.None);

        var listed = await fixture.ListOrders.HandleAsync(CancellationToken.None);

        Assert.Equal([higher.Id, lower.Id], listed.Select(order => order.Id));
        Assert.True(listed[0].Id.CompareTo(listed[1].Id) > 0);
    }
}
