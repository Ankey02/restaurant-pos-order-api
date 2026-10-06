using Microsoft.AspNetCore.Diagnostics;
using RestaurantPos.Application.Common;
using RestaurantPos.Domain.Common;

namespace RestaurantPos.Api.Middleware;

// Turns exceptions into clean JSON error answers.
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "A business rule was broken"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        var detail = status == StatusCodes.Status500InternalServerError
            ? "Something went wrong on our side."
            : exception.Message;

        await Results.Problem(statusCode: status, title: title, detail: detail).ExecuteAsync(context);
        return true;
    }
}
