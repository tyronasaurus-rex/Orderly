using Orderly.Application;
using Orderly.Models;

namespace Orderly.Services;

public static class OrderResponseMapper
{
    public static OrderResponse From(OrderDetails order) =>
        new()
        {
            Id = order.Id,
            ExternalReference = order.ExternalReference,
            CustomerName = order.CustomerName,
            Lines = order.Lines.Select(line => new OrderLineResponse
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
            }).ToArray(),
            Total = order.Total,
            Status = order.Status,
            CreatedAtUtc = order.CreatedAtUtc,
        };
}
