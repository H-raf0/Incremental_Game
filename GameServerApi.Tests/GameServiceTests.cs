using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;

namespace GameServerApi.Tests
{
    public class GameServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ClickAsync_IncrementsCount()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 1;
            context.Progressions.Add(new Progression(userId));
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var response = await service.ClickAsync(userId);

            Assert.Equal(1, response.Count);
            Assert.Equal(1, response.Multiplier);
        }

        [Fact]
        public async Task GetResetCostAsync_ReturnsExpectedCost()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 2;
            var progression = new Progression(userId);
            progression.Multiplier = 1;
            context.Progressions.Add(progression);
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var cost = await service.GetResetCostAsync(userId);

            Assert.Equal(100, cost);
        }

        [Fact]
        public async Task InitializeProgressionAsync_CreatesProgression_WhenNotExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 10;

            var service = new GameService(context, new NullLogger<GameService>());

            var progression = await service.InitializeProgressionAsync(userId);

            Assert.NotNull(progression);
            Assert.Equal(userId, progression.UserId);
        }

        [Fact]
        public async Task InitializeProgressionAsync_Throws_WhenAlreadyExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 11;
            context.Progressions.Add(new Progression(userId));
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameServerApi.Exceptions.GameException>(async () =>
            {
                await service.InitializeProgressionAsync(userId);
            });
        }

        [Fact]
        public async Task ClickAsync_Throws_WhenNoProgression()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameServerApi.Exceptions.GameException>(async () =>
            {
                await service.ClickAsync(999);
            });
        }

        [Fact]
        public async Task ResetProgressionAsync_Succeeds_UpdatesBestScoreAndClearsInventory()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 20;
            var progression = new Progression(userId) { Count = 500, BestScore = 100, totalClickValue = 10, Multiplier = 2 };
            context.Progressions.Add(progression);
            context.InventoryEntries.Add(new InventoryEntry(userId, 1, 2));
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var result = await service.ResetProgressionAsync(userId);

            Assert.Equal(500, result.BestScore);
            Assert.Equal(0, result.Count);
            Assert.Equal(0, result.totalClickValue);
            Assert.Equal(3, result.Multiplier);

            var inventoryCount = await context.InventoryEntries.CountAsync(i => i.UserId == userId);
            Assert.Equal(0, inventoryCount);
        }

        [Fact]
        public async Task ResetProgressionAsync_Throws_WhenInsufficientClicks()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 21;
            var progression = new Progression(userId) { Count = 10, Multiplier = 3 };
            context.Progressions.Add(progression);
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameServerApi.Exceptions.GameException>(async () =>
            {
                await service.ResetProgressionAsync(userId);
            });
        }

        [Fact]
        public async Task GetBestScoreAsync_Throws_WhenNoProgressions()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameServerApi.Exceptions.GameException>(async () =>
            {
                await service.GetBestScoreAsync();
            });
        }

        [Fact]
        public async Task GetBestScoreAsync_ReturnsHighestUser()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(30) { BestScore = 100 });
            context.Progressions.Add(new Progression(31) { BestScore = 300 });
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var best = await service.GetBestScoreAsync();

            Assert.Equal(31, best.UserId);
            Assert.Equal(300, best.BestScore);
        }
    }
}