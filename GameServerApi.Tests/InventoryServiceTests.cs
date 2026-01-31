using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;
using GameServerApi.Exceptions;

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

            var progression = new Progression(user.Id) { Count = 200, TotalClickValue = 0 };
            context.Progressions.Add(progression);

            var item = new Item(1, "TestItem", 50, 10, 2);
            context.Items.Add(item);

            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var entry = await service.BuyItemAsync(user.Id, item.Id);

            Assert.NotNull(entry);
            var updatedProgression = await context.Progressions.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(updatedProgression);
            Assert.Equal(150, updatedProgression!.Count);
            Assert.Equal(2, updatedProgression!.TotalClickValue);
        }

        [Fact]
        public async Task BuyItemAsync_ShouldThrow_WhenNotEnoughMoney()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("tester", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var progression = new Progression(user.Id) { Count = 10 }; // Not enough
            context.Progressions.Add(progression);

            var item = new Item(1, "ExpensiveItem", 100, 10, 5);
            context.Items.Add(item);

            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.BuyItemAsync(user.Id, item.Id);
            });
        }

        [Fact]
        public async Task BuyItemAsync_IncreasesQuantity_WhenItemAlreadyOwned()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("tester", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var progression = new Progression(user.Id) { Count = 200 };
            context.Progressions.Add(progression);

            var item = new Item(1, "TestItem", 50, 10, 2);
            context.Items.Add(item);

            var existingEntry = new InventoryEntry(user.Id, item.Id, 3);
            context.InventoryEntries.Add(existingEntry);

            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var entry = await service.BuyItemAsync(user.Id, item.Id);

            Assert.Equal(4, entry.Quantity); // 3 + 1
        }

        [Fact]
        public async Task GetAllItemsAsync_ReturnsItems_WhenItemsExist()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            context.Items.AddRange(
                new Item(1, "Item1", 10, 5, 1),
                new Item(2, "Item2", 20, 10, 2)
            );
            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var items = await service.GetAllItemsAsync();

            Assert.Equal(2, items.Length);
        }

        [Fact]
        public async Task GetUserInventoryAsync_ReturnsUserItems()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("tester", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.InventoryEntries.AddRange(
                new InventoryEntry(user.Id, 1, 5),
                new InventoryEntry(user.Id, 2, 3)
            );
            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var inventory = await service.GetUserInventoryAsync(user.Id);

            Assert.Equal(2, inventory.Length);
        }

        [Fact]
        public async Task PurchaseItemAsync_DebitsMoneyAndAddsItem()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("buyer", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var progression = new Progression(user.Id) { Count = 100, TotalClickValue = 0 };
            context.Progressions.Add(progression);

            var item = new Item(1, "Cheap", 25, 10, 3);
            context.Items.Add(item);

            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var entry = await service.BuyItemAsync(user.Id, item.Id);

            Assert.NotNull(entry);

            var updatedProgression = await context.Progressions.FirstOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(updatedProgression);
            Assert.Equal(75, updatedProgression!.Count);
            Assert.Equal(3, updatedProgression!.TotalClickValue);

            var invEntry = await context.InventoryEntries.FirstOrDefaultAsync(e => e.UserId == user.Id && e.ItemId == item.Id);
            Assert.NotNull(invEntry);
            Assert.Equal(1, invEntry.Quantity);
        }

        [Fact]
        public async Task BuyItemAsync_Sends_SystemMessage_WhenExpensive()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("richBuyer", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var progression = new Progression(user.Id) { Count = 50000 };
            context.Progressions.Add(progression);

            var item = new Item(1, "LegendarySword", 20000, 1, 100);
            context.Items.Add(item);

            await context.SaveChangesAsync();

            var mockHub = new Moq.Mock<Microsoft.AspNetCore.SignalR.IHubContext<ChatHub>>();
            var mockClients = new Moq.Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
            var mockProxyAll = new Moq.Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);
            mockHub.SetupGet(h => h.Clients).Returns(mockClients.Object);

            var service = new InventoryService(context, new NullLogger<InventoryService>(), mockHub.Object);

            var entry = await service.BuyItemAsync(user.Id, item.Id);

            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("richBuyer vient d'acquérir LegendarySword")),
                    It.IsAny<System.Threading.CancellationToken>()),
                Moq.Times.Once);
        }
    }
}