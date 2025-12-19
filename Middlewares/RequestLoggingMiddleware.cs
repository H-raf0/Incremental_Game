using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace GameServerApi.Middlewares;

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

            _logger.LogDebug("HTTP Request: {Method} {Path}", context.Request.Method, context.Request.Path);

            await _next(context);

            _logger.LogDebug("HTTP Response: {Method} {Path} - Status: {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
}
    
