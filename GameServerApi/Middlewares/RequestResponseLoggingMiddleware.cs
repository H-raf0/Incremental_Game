using System.Diagnostics;
using System.Text;

namespace GameServerApi.Middlewares;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Incoming request {Method} {Path}", context.Request.Method, context.Request.Path);

        // Copy original response body to capture it
        var originalBodyStream = context.Response.Body;
        try
        {
            await _next(context);
            sw.Stop();
            _logger.LogDebug("Request {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
