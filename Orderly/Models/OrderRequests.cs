using Orderly.Application;

namespace Orderly.Models;

public sealed class CreateOrderRequest
{
    public const int ExternalReferenceMaxLength = OrderFieldLimits.ExternalReferenceMaxLength;
    public const int CustomerNameMaxLength = OrderFieldLimits.CustomerNameMaxLength;

    public string? ExternalReference { get; set; }

    public string? CustomerName { get; set; }

    public List<CreateOrderLineRequest>? Lines { get; set; }
}

public sealed class CreateOrderLineRequest
{
    public const int DescriptionMaxLength = OrderFieldLimits.DescriptionMaxLength;

    public string? Description { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

public sealed class ChangeOrderStatusRequest
{
    public string? Status { get; set; }
}
