
namespace GameServerApi;

using Scalar.AspNetCore;
using GameServerApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

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

        builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecific", builder => 
                    builder
                        .WithOrigins("https://csharp.nouvet.fr", "http://localhost:3000", "http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseCors("AllowSpecific");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
