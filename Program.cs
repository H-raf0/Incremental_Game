
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
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, // Max 10 clics par minute par utilisateur
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
            
        });

        var app = builder.Build();

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

        app.Run();
    }
}
