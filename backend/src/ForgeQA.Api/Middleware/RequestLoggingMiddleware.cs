using System.Security.Claims;

namespace ForgeQA.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId
        }))
        {
            await _next(context);

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");

            _logger.LogInformation(
                "Handled {Method} {Path} -> {StatusCode} (UserId: {UserId})",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                userId ?? "anonymous");
        }
    }
}
