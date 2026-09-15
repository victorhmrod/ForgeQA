using ForgeQA.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ForgeQA.Api.Extensions;

public static class ResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);

        return result.ErrorType switch
        {
            ErrorType.NotFound => controller.NotFound(Problem(result, 404)),
            ErrorType.Forbidden => new ObjectResult(Problem(result, 403)) { StatusCode = 403 },
            ErrorType.Unauthorized => new ObjectResult(Problem(result, 401)) { StatusCode = 401 },
            ErrorType.Conflict => new ConflictObjectResult(Problem(result, 409)),
            ErrorType.Validation => controller.BadRequest(Problem(result, 400)),
            ErrorType.Internal => new ObjectResult(Problem(result, 500)) { StatusCode = 500 },
            _ => controller.BadRequest(Problem(result, 400))
        };
    }

    private static ProblemDetails Problem<T>(Result<T> result, int statusCode) => new()
    {
        Status = statusCode,
        Title = result.ErrorType.ToString(),
        Detail = result.Error
    };
}
