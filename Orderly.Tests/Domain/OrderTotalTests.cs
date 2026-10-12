using Orderly.Domain;

namespace Orderly.Tests.Domain;

public class OrderTotalTests
{
    [Fact]
    public void Rounds_half_away_from_zero_to_two_decimal_places()
    {
        Assert.Equal(1.01m, Money.Round(1.005m));
        Assert.Equal(1.00m, Money.Round(1.004m));
    }

    [Fact]
    public void Line_total_is_the_rounded_product()
    {
        var line = OrderLine.Create("Pen", 3, 0.335m, 1);

        Assert.Equal(1.01m, line.LineTotal);
        Assert.Equal(0.335m, line.UnitPrice);
    }

    [Fact]
    public void Order_total_is_the_rounded_sum_of_line_totals()
    {
        var order = Order.Create(
            "PO-1",
            "Northwind",
            [
                new NewOrderLine("Pen", 3, 0.335m),
                new NewOrderLine("Clip", 1, 1.004m),
            ],
            DateTimeOffset.UnixEpoch);

        Assert.Equal(1.01m, order.Lines[0].LineTotal);
        Assert.Equal(1.00m, order.Lines[1].LineTotal);
        Assert.Equal(2.01m, order.Total);
    }

    [Fact]
    public void Zero_price_is_kept()
    {
        var order = Order.Create(
            "PO-1",
            "Northwind",
            [new NewOrderLine("Sample", 4, 0m)],
            DateTimeOffset.UnixEpoch);

        Assert.Equal(0m, order.Lines[0].UnitPrice);
        Assert.Equal(0m, order.Lines[0].LineTotal);
        Assert.Equal(0m, order.Total);
    }

    [Fact]
    public void Binder_example_totals_seven()
    {
        var order = Order.Create(
            "PO-1001",
            "Northwind",
            [new NewOrderLine("Binder", 2, 3.5m)],
            DateTimeOffset.UnixEpoch);

        Assert.Equal(7.00m, order.Lines[0].LineTotal);
        Assert.Equal(7.00m, order.Total);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.NotEqual(Guid.Empty, order.Id);
    }
}
