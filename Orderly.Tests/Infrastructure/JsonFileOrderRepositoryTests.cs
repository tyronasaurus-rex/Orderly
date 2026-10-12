using Orderly.Application;
using Orderly.Infrastructure;
using Orderly.Tests.Support;

namespace Orderly.Tests.Infrastructure;

public class JsonFileOrderRepositoryTests
{
    [Fact]
    public async Task Missing_or_empty_file_means_there_are_no_orders()
    {
        using var fixture = new OrderFixture();
        Assert.Empty(await fixture.Repository.ListAsync(CancellationToken.None));

        await File.WriteAllTextAsync(fixture.OrdersFile, "  ");
        Assert.Empty(await fixture.Repository.ListAsync(CancellationToken.None));

        await File.WriteAllTextAsync(fixture.OrdersFile, "[]");
        Assert.Empty(await fixture.Repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_update_leaves_the_previous_file_intact()
    {
        using var fixture = new OrderFixture();
        await fixture.SubmitOrder.HandleAsync(fixture.Command(), CancellationToken.None);
        var before = await File.ReadAllTextAsync(fixture.OrdersFile);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Repository.UpdateAsync<int>(_ => throw new InvalidOperationException("fail"), CancellationToken.None));

        Assert.Equal(before, await File.ReadAllTextAsync(fixture.OrdersFile));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public async Task Stored_totals_are_recomputed_from_quantity_and_unit_price()
    {
        using var fixture = new OrderFixture();
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await File.WriteAllTextAsync(fixture.OrdersFile, """
            [
              {
                "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "externalReference": "PO-1001",
                "customerName": "Northwind",
                "lines": [
                  { "description": "Binder", "quantity": 2, "unitPrice": 3.5, "lineTotal": 999 }
                ],
                "total": 999,
                "status": "Submitted",
                "createdAtUtc": "2026-10-12T08:00:00+00:00"
              }
            ]
            """);

        var order = await fixture.GetOrder.HandleAsync(id, CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal(7.00m, order.Lines[0].LineTotal);
        Assert.Equal(7.00m, order.Total);
    }

    [Fact]
    public async Task Successful_write_replaces_the_file_without_leaving_a_temp_file()
    {
        using var fixture = new OrderFixture();
        await fixture.SubmitOrder.HandleAsync(fixture.Command(), CancellationToken.None);

        await fixture.SubmitOrder.HandleAsync(fixture.Command("PO-2002", "Contoso"), CancellationToken.None);

        Assert.True(File.Exists(fixture.OrdersFile));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));

        using var reread = new JsonFileOrderRepository(fixture.OrdersFile);
        var listed = await reread.ListAsync(CancellationToken.None);
        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, order => order.ExternalReference == "PO1001");
        Assert.Contains(listed, order => order.ExternalReference == "PO2002");
    }
}

public class OrdersFileLocationTests
{
    [Fact]
    public void Default_path_stays_under_the_content_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "orderly-root", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var resolved = OrdersFileLocation.Resolve(root, null);

        Assert.Equal(Path.Combine(root, "data", "orders.json"), resolved);
        Directory.Delete(root);
    }

    [Fact]
    public void Path_outside_the_content_root_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "orderly-root", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var outside = Path.GetFullPath(Path.Combine(root, "..", "orders.json"));
        Assert.Throws<InvalidOperationException>(() => OrdersFileLocation.Resolve(root, outside));
        Assert.Throws<InvalidOperationException>(() => OrdersFileLocation.Resolve(root, "../orders.json"));

        Directory.Delete(root);
    }
}
