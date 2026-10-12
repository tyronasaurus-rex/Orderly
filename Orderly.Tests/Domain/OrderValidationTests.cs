using Orderly.Domain;

namespace Orderly.Tests.Domain;

public class OrderValidationTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("#")]
    public void Missing_reference_is_rejected(string? reference)
    {
        var exception = Assert.Throws<OrderValidationException>(() =>
            Order.Create(reference, "Northwind", [new NewOrderLine("Binder", 1, 1m)], CreatedAt));

        Assert.Equal("External reference is required.", exception.Message);
    }

    [Fact]
    public void Reference_longer_than_64_characters_is_rejected()
    {
        var reference = new string('A', OrderFieldLimits.ExternalReferenceMaxLength + 1);
        var exception = Assert.Throws<OrderValidationException>(() =>
            Order.Create(reference, "Northwind", [new NewOrderLine("Binder", 1, 1m)], CreatedAt));

        Assert.Contains(OrderFieldLimits.ExternalReferenceMaxLength.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_customer_is_rejected(string? customer)
    {
        var exception = Assert.Throws<OrderValidationException>(() =>
            Order.Create("PO-1", customer, [new NewOrderLine("Binder", 1, 1m)], CreatedAt));

        Assert.Equal("Customer name is required.", exception.Message);
    }

    [Fact]
    public void Missing_lines_are_rejected()
    {
        var exception = Assert.Throws<OrderValidationException>(() =>
            Order.Create("PO-1", "Northwind", [], CreatedAt));

        Assert.Equal("An order must include at least one line.", exception.Message);
    }

    [Fact]
    public void Missing_line_description_is_rejected()
    {
        var exception = Assert.Throws<OrderValidationException>(() =>
            Order.Create("PO-1", "Northwind", [new NewOrderLine("  ", 1, 1m)], CreatedAt));

        Assert.Equal("Line 1 description is required.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.5)]
    public void Quantity_must_be_a_positive_whole_number(decimal quantity)
    {
        var exception = Assert.Throws<OrderValidationException>(() =>
            OrderLine.Create("Binder", quantity, 1m, 1));

        Assert.Equal("Line 1 quantity must be a positive whole number.", exception.Message);
    }

    [Fact]
    public void Negative_price_is_rejected()
    {
        var exception = Assert.Throws<OrderValidationException>(() =>
            OrderLine.Create("Binder", 1, -0.01m, 1));

        Assert.Equal("Line 1 unit price must be zero or greater.", exception.Message);
    }

    [Fact]
    public void Reference_and_customer_are_trimmed_to_the_maximum_length()
    {
        var reference = " " + new string('R', OrderFieldLimits.ExternalReferenceMaxLength) + " ";
        var customer = " " + new string('C', OrderFieldLimits.CustomerNameMaxLength) + " ";

        var order = Order.Create(reference, customer, [new NewOrderLine(" Binder ", 1, 1m)], CreatedAt);

        Assert.Equal(OrderFieldLimits.ExternalReferenceMaxLength, order.ExternalReference.Length);
        Assert.Equal(OrderFieldLimits.CustomerNameMaxLength, order.CustomerName.Length);
        Assert.Equal("Binder", order.Lines[0].Description);
    }

    [Fact]
    public void Reference_keeps_only_letters_and_digits()
    {
        var order = Order.Create(" po-#1001 ", "Northwind", [new NewOrderLine("Binder", 1, 1m)], CreatedAt);

        Assert.Equal("po1001", order.ExternalReference);
    }
}
