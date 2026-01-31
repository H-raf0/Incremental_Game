
namespace GameServerApi;

using Scalar.AspNetCore;
using GameServerApi.Models;
using GameServerApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using GameServerApi.Middlewares;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Ajouter les services SignalR
        builder.Services.AddSignalR();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.FromMinutes(10), // Temps de tolérance pour la date d'expiration
                    ValidateLifetime = true, // Vérifie la date d'expiration
                    ValidateIssuerSigningKey = true, // Vérifie la signature
                    ValidAudience = "localhost:5000", // Qui peut utiliser le token ici c'est notre API
                    ValidIssuer = "localhost:5000", // Qui émet le token ici c'est notre API
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("TheSecretKeyThatShouldBeStoredInTheConfiguration")
                    ),
                    RoleClaimType = ClaimTypes.Role // Dans quel claim est stocké le role
                };

                // Handle authorization and authentication failures for JWT
                options.Events = new JwtBearerEvents
                {
                    // Support receiving the access token from the query string for SignalR (WebSockets)
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"].ToString();
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub/chat"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var errorResponse = new { error = "Unauthorized", code = "UNAUTHORIZED", message = "Authentication required or token invalid" };
                        await context.Response.WriteAsJsonAsync(errorResponse);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        var errorResponse = new { error = "Forbidden", code = "FORBIDDEN", message = "You do not have permission to access this resource" };
                        await context.Response.WriteAsJsonAsync(errorResponse);
                    }
                };
            });
        builder.Services.AddAuthorization();

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddDbContext<ApplicationDbContext>();

        builder.Services.AddScoped<PasswordHasher<User>>();
        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<GameService>();
        builder.Services.AddScoped<InventoryService>();
        builder.Services.AddScoped<PassiveIncomeService>();
        builder.Services.AddSingleton<ConnectionTrackerService>();

        // Register the background service for passive income
        builder.Services.AddHostedService<MyWorker>();

        builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecific", builder =>
                    builder
                        .WithOrigins("https://csharp.nouvet.fr", "http://localhost:3000", "http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
            });
        /*
        builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.SetIsOriginAllowed(origin => true) // Allow any origin
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials(); // SignalR requires credentials
                });
            });
        */

        builder.Services.AddRateLimiter(options =>
        {
            // Rejet avec le code 429 Too Many Requests
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                var errorResponse = new { error = "Too Many Requests", code = "TOO_MANY_REQUESTS", message = "Rate limit exceeded" };
                await context.HttpContext.Response.WriteAsJsonAsync(errorResponse, token);
            };

            // Définition d'une politique nommée "fixed"
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10000; // Max requêtes par l'ensemble des utilisateurs
                limiterOptions.Window = TimeSpan.FromSeconds(10); // Toutes les 10 secondes
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // Pas de file d'attente
            });

            // Politique par utilisateur pour les clics
            options.AddPolicy("perUser", context =>
            {
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, // Max 10 clics par seconde par utilisateur
                    Window = TimeSpan.FromSeconds(10),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

        });

        var app = builder.Build();

        app.Logger.LogInformation("Application is starting up...");

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }
        app.UseCors("AllowSpecific");
        app.UseRateLimiter();
        app.UseMiddleware<RequestResponseLoggingMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/hub/chat");

        // Handle authorization policy failures with JSON response
        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
            {
                context.Response.ContentType = "application/json";
                var errorResponse = new { error = "Forbidden", code = "FORBIDDEN", message = "You do not have permission to access this resource" };
                await context.Response.WriteAsJsonAsync(errorResponse);
            }
            else if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                context.Response.ContentType = "application/json";
                var errorResponse = new { error = "Unauthorized", code = "UNAUTHORIZED", message = "Authentication required or token invalid" };
                await context.Response.WriteAsJsonAsync(errorResponse);
            }
        });

        app.MapControllers();

        app.Logger.LogInformation("Application initialization complete");

        app.Run();
    }
}
