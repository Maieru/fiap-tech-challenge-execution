using FIAP.TechChallenge.Execution.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.TechChallenge.Execution.API.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            if (result.Value is null)
                return controller.NoContent();

            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value);
        }

        var errorResponse = new ErrorActionResponse(
            result.Error.Description,
            result.Error.Code.ToString());

        return controller.StatusCode(ToStatusCode(result.Error.Code), errorResponse);
    }

    private static int ToStatusCode(ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.BadRequest => StatusCodes.Status400BadRequest,
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCode.Conflict => StatusCodes.Status409Conflict,
        ErrorCode.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
        ErrorCode.InternalServerError => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError
    };

    private sealed record ErrorActionResponse(string Error, string ErrorCode);
}
