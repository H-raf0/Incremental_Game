
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
using System.Threading.RateLimiting;
using GameServerApi.Middlewares;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuration JWT
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
                
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.FromMinutes(5), // 5 min de tolérance
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAudience = jwtSettings?.Audience ?? "incremental-game-client",
                    ValidIssuer = jwtSettings?.Issuer ?? "incremental-game-api",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings?.Key ?? string.Empty)
                    ),
                    RoleClaimType = ClaimTypes.Role
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

        // Enregistrer le service de revenu passif
        builder.Services.AddHostedService<PassiveIncomeService>();

        builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecific", builder => 
                    builder
                        .WithOrigins("https://csharp.nouvet.fr", "http://localhost:3000", "http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
            });
        
        builder.Services.AddRateLimiter(options =>
        {
            // Rejet avec le code 429 Too Many Requests
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync("{\"error\": \"Too Many Requests\", \"message\": \"Rate limit exceeded\"}", token);
            };

            // Définition d'une politique nommée "fixed"
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10; // Max 10 requêtes
                limiterOptions.Window = TimeSpan.FromSeconds(10); // Toutes les 10 secondes
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0; // Pas de file d'attente
            });

            // Politique par utilisateur pour les clics
            options.AddPolicy("perUser", context =>
            {
                var username = context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString();
                return RateLimitPartition.GetFixedWindowLimiter(username, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
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
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Logger.LogInformation("Application initialization complete");

        app.Run();
    }
}
