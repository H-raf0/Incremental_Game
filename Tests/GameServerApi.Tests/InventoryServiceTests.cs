using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;
using System.Net.Http;
using System.Net;

namespace GameServerApi.Tests
{
    public class InventoryServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
            {
                return new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            }
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

            var httpFactory = new TestHttpClientFactory();
            var service = new InventoryService(context, httpFactory, new NullLogger<InventoryService>());

            var entry = await service.BuyItemAsync(user.Id, item.Id);

            Assert.NotNull(entry);
            var updatedProgression = await context.Progressions.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.Equal(150, updatedProgression.Count);
            Assert.Equal(2, updatedProgression.totalClickValue);
        }
    }
}
