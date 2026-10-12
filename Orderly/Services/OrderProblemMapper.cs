using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Orderly.Services;

public static class OrderProblemCodes
{
    public const string ValidationError = "validation_error";
    public const string DuplicateReference = "duplicate_reference";
    public const string OrderNotFound = "order_not_found";
    public const string InvalidStatusTransition = "invalid_status_transition";
    public const string Unexpected = "error";
}

public static class OrderProblemMapper
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public static JsonResult ToResult(int status, string code, string detail) =>
        new(CreateProblem(status, code, detail))
        {
            StatusCode = status,
            ContentType = "application/problem+json",
        };

    public static IActionResult InvalidModelState(ActionContext context)
    {
        Log(context.HttpContext, orderId: null, OrderProblemCodes.ValidationError);
        return ToResult(
            StatusCodes.Status400BadRequest,
            OrderProblemCodes.ValidationError,
            "The request body is not valid.");
    }

    public static void HandleUnexpected(IApplicationBuilder app)
    {
        app.Run(async context =>
        {
            Log(context, TryReadOrderId(context), OrderProblemCodes.Unexpected);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            await context.Response.WriteAsJsonAsync(
                CreateProblem(
                    StatusCodes.Status500InternalServerError,
                    OrderProblemCodes.Unexpected,
                    "The request could not be completed."),
                WebJson,
                context.RequestAborted);
        });
    }

    public static void Log(HttpContext context, Guid? orderId, string code)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Orderly.Orders");
        if (orderId is Guid id)
            logger.LogWarning("Order {OrderId} failed with {Code}", id, code);
        else
            logger.LogWarning("Request failed with {Code}", code);
    }

    private static ProblemDetails CreateProblem(int status, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(code),
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static string TitleFor(string code) => code switch
    {
        OrderProblemCodes.ValidationError => "The order is not valid.",
        OrderProblemCodes.DuplicateReference => "The external reference is already used.",
        OrderProblemCodes.OrderNotFound => "The order was not found.",
        OrderProblemCodes.InvalidStatusTransition => "The status change is not allowed.",
        _ => "An unexpected error occurred.",
    };

    private static Guid? TryReadOrderId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("id", out var value)
            && value is string text
            && Guid.TryParse(text, out var id))
        {
            return id;
        }

        return null;
    }
}
