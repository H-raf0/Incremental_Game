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
    }
}
