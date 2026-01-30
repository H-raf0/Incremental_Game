using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit;
using GameServerApi.Models;
using GameServerApi.Services;

namespace GameServerApi.Tests.Integration;

public class InventoryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace ApplicationDbContext with InMemory provider
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTestDb")
                           .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });
    }

    [Fact]
    public async Task BuyItemEndpoint_DebitsMoneyAndAddsItem()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<ApplicationDbContext>();

        var user = new User("intBuyer", "pwd", Role.USER);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var progression = new Progression(user.Id) { Count = 200, totalClickValue = 0 };
        db.Progressions.Add(progression);

        var item = new Item(1, "IntegrationItem", 50, 10, 5);
        db.Items.Add(item);

        await db.SaveChangesAsync();

        // Generate token using JwtService configured like app
        var jwtSettings = _factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var jwtService = new JwtService(Options.Create(jwtSettings), scopedServices.GetRequiredService<ILogger<JwtService>>());

        var tokenResponse = jwtService.GenerateTokens(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

        // Act
        var response = await client.PostAsync($"/api/Inventory/Buy/{item.Id}", null);

        response.EnsureSuccessStatusCode();

        var entry = await response.Content.ReadFromJsonAsync<InventoryEntry>();

        // Assert
        Assert.NotNull(entry);

        // Use a fresh scope/db context to read updated values (avoid cached tracked entities)
        using var readScope = _factory.Services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var updatedProgression = await readDb.Progressions.FirstOrDefaultAsync(p => p.UserId == user.Id);
        Assert.NotNull(updatedProgression);
        Assert.Equal(150, updatedProgression!.Count);
        Assert.Equal(5, updatedProgression.totalClickValue);

        var invEntry = await readDb.InventoryEntries.FirstOrDefaultAsync(e => e.UserId == user.Id && e.ItemId == item.Id);
        Assert.NotNull(invEntry);
        Assert.Equal(1, invEntry.Quantity);
    }
}