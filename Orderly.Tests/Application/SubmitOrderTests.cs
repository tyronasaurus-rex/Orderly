using Orderly.Application;
using Orderly.Tests.Support;

namespace Orderly.Tests.Application;

public class SubmitOrderTests
{
    [Fact]
    public async Task First_submit_stores_one_submitted_order_with_server_totals()
    {
        using var fixture = new OrderFixture();

        var result = await fixture.SubmitOrder.HandleAsync(fixture.Command(), CancellationToken.None);

        var created = Assert.IsType<SubmitOrderResult.Created>(result);
        Assert.Equal("Submitted", created.Order.Status);
        Assert.Equal(7.00m, created.Order.Total);
        Assert.Equal(7.00m, created.Order.Lines[0].LineTotal);
        Assert.NotEqual(Guid.Empty, created.Order.Id);

        var stored = await fixture.GetOrder.HandleAsync(created.Order.Id, CancellationToken.None);
        Assert.Equal(created.Order.Id, stored!.Id);
        Assert.Equal(7.00m, stored.Total);
    }

    [Fact]
    public async Task Equivalent_resubmit_returns_the_same_order_and_current_status()
    {
        using var fixture = new OrderFixture();
        var created = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(fixture.Command(" PO-1001 "), CancellationToken.None));
        var confirmed = Assert.IsType<ChangeOrderStatusResult.Updated>(
            await fixture.ChangeOrderStatus.HandleAsync(created.Order.Id, "Confirmed", CancellationToken.None));

        var repeat = await fixture.SubmitOrder.HandleAsync(
            fixture.Command("PO-1001", " Northwind ", new SubmitOrderLine(" Binder ", 2, 3.50m)),
            CancellationToken.None);

        var existing = Assert.IsType<SubmitOrderResult.Existing>(repeat);
        Assert.Equal(created.Order.Id, existing.Order.Id);
        Assert.Equal(confirmed.Order.Status, existing.Order.Status);
        Assert.Equal(7.00m, existing.Order.Total);
        Assert.Single(await fixture.ListOrders.HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Prices_that_round_to_the_same_value_are_the_same_submission()
    {
        using var fixture = new OrderFixture();
        var created = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(
                fixture.Command("PO-1", "Northwind", new SubmitOrderLine("Pen", 3, 1.005m)),
                CancellationToken.None));

        var repeat = await fixture.SubmitOrder.HandleAsync(
            fixture.Command("PO-1", "Northwind", new SubmitOrderLine("Pen", 3, 1.01m)),
            CancellationToken.None);

        var existing = Assert.IsType<SubmitOrderResult.Existing>(repeat);
        Assert.Equal(created.Order.Id, existing.Order.Id);
        Assert.Equal(3.02m, existing.Order.Total);
        Assert.Single(await fixture.ListOrders.HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Different_content_for_the_same_reference_does_not_insert_or_mutate()
    {
        using var fixture = new OrderFixture();
        var created = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(fixture.Command(), CancellationToken.None));

        var conflict = await fixture.SubmitOrder.HandleAsync(
            fixture.Command("PO-1001", "Contoso", new SubmitOrderLine("Binder", 2, 3.5m)),
            CancellationToken.None);

        var rejected = Assert.IsType<SubmitOrderResult.ReferenceInUse>(conflict);
        Assert.Equal(created.Order.Id, rejected.OrderId);
        Assert.Equal(SubmitOrder.ReferenceInUseDetail, rejected.Detail);

        var stored = await fixture.GetOrder.HandleAsync(created.Order.Id, CancellationToken.None);
        Assert.Equal("Northwind", stored!.CustomerName);
        Assert.Equal(7.00m, stored.Total);
        Assert.Equal("Submitted", stored.Status);
        Assert.Single(await fixture.ListOrders.HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Swapped_lines_are_a_different_submission()
    {
        using var fixture = new OrderFixture();
        var lines = new[]
        {
            new SubmitOrderLine("Binder", 1, 2m),
            new SubmitOrderLine("Pen", 1, 3m),
        };
        await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-1", "Northwind", lines), CancellationToken.None);

        var swapped = await fixture.SubmitOrder.HandleAsync(
            fixture.Command("PO-1", "Northwind", lines[1], lines[0]),
            CancellationToken.None);

        Assert.IsType<SubmitOrderResult.ReferenceInUse>(swapped);
        var stored = Assert.Single(await fixture.ListOrders.HandleAsync(CancellationToken.None));
        Assert.Equal(["Binder", "Pen"], stored.Lines.Select(line => line.Description));
    }

    [Fact]
    public async Task Reference_match_ignores_case_and_symbols()
    {
        using var fixture = new OrderFixture();
        var created = Assert.IsType<SubmitOrderResult.Created>(
            await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-1001"), CancellationToken.None));

        var repeat = await fixture.SubmitOrder.HandleAsync(
            fixture.Command("po #1001"),
            CancellationToken.None);

        var existing = Assert.IsType<SubmitOrderResult.Existing>(repeat);
        Assert.Equal(created.Order.Id, existing.Order.Id);
        Assert.Equal("PO1001", existing.Order.ExternalReference);
        Assert.Single(await fixture.ListOrders.HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Different_numbers_remain_different_references()
    {
        using var fixture = new OrderFixture();
        await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-1001"), CancellationToken.None);
        await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-1002"), CancellationToken.None);

        Assert.Equal(2, (await fixture.ListOrders.HandleAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Invalid_order_writes_nothing()
    {
        using var fixture = new OrderFixture();

        var result = await fixture.SubmitOrder.HandleAsync(
            fixture.Command(" ", "Northwind", new SubmitOrderLine("Binder", 0, 1m)),
            CancellationToken.None);

        Assert.IsType<SubmitOrderResult.Invalid>(result);
        Assert.False(File.Exists(fixture.OrdersFile));
        Assert.Empty(await fixture.ListOrders.HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_equivalent_submits_store_one_order()
    {
        using var fixture = new OrderFixture();
        var command = fixture.Command();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => fixture.SubmitOrder.HandleAsync(command, CancellationToken.None)));

        Assert.Single(results.OfType<SubmitOrderResult.Created>());
        Assert.Equal(7, results.OfType<SubmitOrderResult.Existing>().Count());
        Assert.Single(await fixture.ListOrders.HandleAsync(CancellationToken.None));
    }
}
