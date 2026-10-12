using Microsoft.AspNetCore.Mvc;
using Orderly.Application;
using Orderly.Models;
using Orderly.Services;

namespace Orderly.Controllers;

[ApiController]
[Route("api/orders")]
[RequestSizeLimit(OrderRequestLimits.MaxBodyBytes)]
public sealed class OrdersController(
    SubmitOrder submitOrder,
    GetOrder getOrder,
    ListOrders listOrders,
    ChangeOrderStatus changeOrderStatus,
    ILogger<OrdersController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await submitOrder.HandleAsync(ToCommand(request), cancellationToken);
        return result switch
        {
            SubmitOrderResult.Created created => CreatedAtAction(
                nameof(Get),
                new { id = created.Order.Id },
                OrderResponseMapper.From(created.Order)),
            SubmitOrderResult.Existing existing => Ok(OrderResponseMapper.From(existing.Order)),
            SubmitOrderResult.ReferenceInUse conflict => Reject(
                conflict.OrderId,
                StatusCodes.Status409Conflict,
                OrderProblemCodes.DuplicateReference,
                conflict.Detail),
            SubmitOrderResult.Invalid invalid => Reject(
                orderId: null,
                StatusCodes.Status400BadRequest,
                OrderProblemCodes.ValidationError,
                invalid.Detail),
            _ => UnexpectedResult(),
        };
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var orders = await listOrders.HandleAsync(cancellationToken);
        return Ok(orders.Select(OrderResponseMapper.From).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await getOrder.HandleAsync(id, cancellationToken);
        if (order is null)
        {
            return Reject(
                id,
                StatusCodes.Status404NotFound,
                OrderProblemCodes.OrderNotFound,
                OrderText.NotFound(id));
        }

        return Ok(OrderResponseMapper.From(order));
    }

    [HttpPatch("{id:guid}/status")]
    [Consumes("application/json")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await changeOrderStatus.HandleAsync(id, request.Status, cancellationToken);
        return result switch
        {
            ChangeOrderStatusResult.Updated updated => Ok(OrderResponseMapper.From(updated.Order)),
            ChangeOrderStatusResult.NotFound missing => Reject(
                missing.OrderId,
                StatusCodes.Status404NotFound,
                OrderProblemCodes.OrderNotFound,
                missing.Detail),
            ChangeOrderStatusResult.TransitionRejected rejected => Reject(
                rejected.OrderId,
                StatusCodes.Status409Conflict,
                OrderProblemCodes.InvalidStatusTransition,
                rejected.Detail),
            ChangeOrderStatusResult.Invalid invalid => Reject(
                id,
                StatusCodes.Status400BadRequest,
                OrderProblemCodes.ValidationError,
                invalid.Detail),
            _ => UnexpectedResult(),
        };
    }

    private IActionResult Reject(Guid? orderId, int status, string code, string detail)
    {
        if (orderId is Guid id)
            logger.LogWarning("Order {OrderId} failed with {Code}", id, code);
        else
            logger.LogWarning("Request failed with {Code}", code);

        return OrderProblemMapper.ToResult(status, code, detail);
    }

    private IActionResult UnexpectedResult()
    {
        logger.LogWarning("Request failed with {Code}", OrderProblemCodes.Unexpected);
        return OrderProblemMapper.ToResult(
            StatusCodes.Status500InternalServerError,
            OrderProblemCodes.Unexpected,
            "The request could not be completed.");
    }

    private static SubmitOrderCommand ToCommand(CreateOrderRequest request)
    {
        SubmitOrderLine[]? lines = null;
        if (request.Lines is not null)
        {
            lines = new SubmitOrderLine[request.Lines.Count];
            for (var index = 0; index < request.Lines.Count; index++)
            {
                var line = request.Lines[index];
                lines[index] = line is null
                    ? new SubmitOrderLine(null, 0, 0)
                    : new SubmitOrderLine(line.Description, line.Quantity, line.UnitPrice);
            }
        }

        return new SubmitOrderCommand(request.ExternalReference, request.CustomerName, lines);
    }
}
