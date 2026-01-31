using System.Text.Json;
using GameServerApi.Exceptions;
using GameServerApi.Models;
using Microsoft.Extensions.Logging;

namespace GameServerApi.Middlewares;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Handles exceptions and formats error responses.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="exception">The exception to handle.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (exception is GameException gameEx)
        {
            context.Response.StatusCode = gameEx.StatusCode;
            var errorResponse = new ErrorResponse(gameEx.Message, gameEx.Code);
            _logger.LogError(exception, "GameException handled: {Code} - {Message}", gameEx.Code, gameEx.Message);

            // JsonSerializer.Serialize est appelé automatiquement par WriteAsJsonAsync
            await context.Response.WriteAsJsonAsync(errorResponse, options);
        }
        else
        {
            context.Response.StatusCode = 500;
            _logger.LogError(exception, "An unhandled exception occurred");
            var errorResponse = new ErrorResponse("Internal Server Error", "INTERNAL_SERVER_ERROR");
            await context.Response.WriteAsJsonAsync(errorResponse, options);
        }
    }

    /// <summary>
    /// Invokes the middleware to handle requests and exceptions.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
}