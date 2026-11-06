using Microsoft.AspNetCore.Mvc;
using ShopService.Domain.Common;

namespace ShopService.Api.Extensions;

internal static class ControllerExtensions
{
    public const string ErrorCodeExtension = "code";

    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblem(result.Error);

    public static ActionResult<T> ToCreated<T>(this ControllerBase controller, Result<T> result, string actionName, Func<T, object> routeValues) =>
        result.IsSuccess ? controller.CreatedAtAction(actionName, routeValues(result.Value), result.Value) : controller.ToProblem(result.Error);

    public static ActionResult ToNoContent(this ControllerBase controller, Result result) =>
        result.IsSuccess ? controller.NoContent() : controller.ToProblem(result.Error);

    public static ActionResult ToProblem(this ControllerBase controller, Error error)
    {
        ObjectResult problem;
        if (error is { Type: ErrorType.Validation, Details: not null })
        {
            foreach (var (key, messages) in error.Details)
            {
                foreach (var message in messages)
                {
                    controller.ModelState.AddModelError(key, message);
                }
            }

            problem = (ObjectResult)controller.ValidationProblem(controller.ModelState);
        }
        else
        {
            var statusCode = error.Type switch
            {
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            problem = controller.Problem(detail: error.Message, statusCode: statusCode);
        }

        ((ProblemDetails)problem.Value!).Extensions[ErrorCodeExtension] = error.Code;
        return problem;
    }
}
