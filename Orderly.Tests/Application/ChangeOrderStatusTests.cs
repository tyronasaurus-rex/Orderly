using Orderly.Application;
using Orderly.Tests.Support;

namespace Orderly.Tests.Application;

public class ChangeOrderStatusTests
{
    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Cancelled")]
    public async Task Submitted_can_move_to_an_allowed_status(string status)
    {
        using var fixture = new OrderFixture();
        var created = await Create(fixture);

        var updated = Assert.IsType<ChangeOrderStatusResult.Updated>(
            await fixture.ChangeOrderStatus.HandleAsync(created, status, CancellationToken.None));

        Assert.Equal(status, updated.Order.Status);
        Assert.Equal(status, (await fixture.GetOrder.HandleAsync(created, CancellationToken.None))!.Status);
    }

    [Theory]
    [InlineData("Fulfilled")]
    [InlineData("Cancelled")]
    public async Task Confirmed_can_move_to_an_allowed_status(string status)
    {
        using var fixture = new OrderFixture();
        var created = await Create(fixture);
        await fixture.ChangeOrderStatus.HandleAsync(created, "Confirmed", CancellationToken.None);

        var updated = Assert.IsType<ChangeOrderStatusResult.Updated>(
            await fixture.ChangeOrderStatus.HandleAsync(created, status, CancellationToken.None));

        Assert.Equal(status, updated.Order.Status);
    }

    [Fact]
    public async Task Illegal_transition_names_the_current_status_and_leaves_it_unchanged()
    {
        using var fixture = new OrderFixture();
        var created = await Create(fixture);

        var rejected = Assert.IsType<ChangeOrderStatusResult.TransitionRejected>(
            await fixture.ChangeOrderStatus.HandleAsync(created, "Fulfilled", CancellationToken.None));

        Assert.Equal(
            $"Order {created:D} is Submitted. You can change it to Confirmed or Cancelled.",
            rejected.Detail);
        Assert.Equal("Submitted", (await fixture.GetOrder.HandleAsync(created, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task Confirmed_order_lists_fulfilled_and_cancelled()
    {
        using var fixture = new OrderFixture();
        var created = await Create(fixture);
        await fixture.ChangeOrderStatus.HandleAsync(created, "Confirmed", CancellationToken.None);

        var rejected = Assert.IsType<ChangeOrderStatusResult.TransitionRejected>(
            await fixture.ChangeOrderStatus.HandleAsync(created, "Submitted", CancellationToken.None));

        Assert.Equal(
            $"Order {created:D} is Confirmed. You can change it to Fulfilled or Cancelled.",
            rejected.Detail);
        Assert.Equal("Confirmed", (await fixture.GetOrder.HandleAsync(created, CancellationToken.None))!.Status);
    }

    [Theory]
    [InlineData("Fulfilled")]
    [InlineData("Cancelled")]
    public async Task Terminal_status_rejects_further_changes(string terminal)
    {
        using var fixture = new OrderFixture();
        var created = await Create(fixture);
        if (terminal == "Fulfilled")
            await fixture.ChangeOrderStatus.HandleAsync(created, "Confirmed", CancellationToken.None);

        await fixture.ChangeOrderStatus.HandleAsync(created, terminal, CancellationToken.None);
        var rejected = Assert.IsType<ChangeOrderStatusResult.TransitionRejected>(
            await fixture.ChangeOrderStatus.HandleAsync(created, "Confirmed", CancellationToken.None));

        Assert.Equal(
            $"Order {created:D} is {terminal}. No further status changes are allowed.",
            rejected.Detail);
        Assert.Equal(terminal, (await fixture.GetOrder.HandleAsync(created, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task Unknown_status_is_rejected_without_a_write()
    {
        using var fixture = new OrderFixture();
        var created = await Create(fixture);
        var before = File.ReadAllText(fixture.OrdersFile);

        var rejected = Assert.IsType<ChangeOrderStatusResult.Invalid>(
            await fixture.ChangeOrderStatus.HandleAsync(created, "Shipped", CancellationToken.None));

        Assert.Equal("Status must be Submitted, Confirmed, Fulfilled, or Cancelled.", rejected.Detail);
        Assert.Equal(before, File.ReadAllText(fixture.OrdersFile));
        Assert.Equal("Submitted", (await fixture.GetOrder.HandleAsync(created, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task Missing_order_is_reported_and_the_store_stays_empty()
    {
        using var fixture = new OrderFixture();
        var missing = Guid.NewGuid();

        var result = Assert.IsType<ChangeOrderStatusResult.NotFound>(
            await fixture.ChangeOrderStatus.HandleAsync(missing, "Confirmed", CancellationToken.None));

        Assert.Equal(OrderText.NotFound(missing), result.Detail);
        Assert.False(File.Exists(fixture.OrdersFile));
    }

    private static async Task<Guid> Create(OrderFixture fixture)
    {
        var created = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(fixture.Command(), CancellationToken.None));
        return created.Order.Id;
    }
}
