using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;

namespace GameServerApi.Tests
{
    public class InventoryServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task BuyItemAsync_CreatesInventoryEntryAndUpdatesProgression()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("tester", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var progression = new Progression(user.Id) { Count = 200, totalClickValue = 0 };
            context.Progressions.Add(progression);

            var item = new Item(1, "TestItem", 50, 10, 2);
            context.Items.Add(item);

            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var entry = await service.BuyItemAsync(user.Id, item.Id);

            Assert.NotNull(entry);
            var updatedProgression = await context.Progressions.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(updatedProgression);
            Assert.Equal(150, updatedProgression.Count);
            Assert.Equal(2, updatedProgression.totalClickValue);
        }
    }
}
