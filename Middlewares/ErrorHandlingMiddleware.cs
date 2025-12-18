using System.Text.Json;
using GameServerApi.Exceptions;
using GameServerApi.Models;

namespace GameServerApi.Middlewares;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

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

            //JsonSerializer.Serialize est appelé automatiquement par WriteAsJsonAsync
            await context.Response.WriteAsJsonAsync(errorResponse, options);
        }
        else
        {
            context.Response.StatusCode = 500;
            var errorResponse = new ErrorResponse("Internal Server Error", "INTERNAL_SERVER_ERROR");
            await context.Response.WriteAsJsonAsync(errorResponse, options);
        }
    }

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