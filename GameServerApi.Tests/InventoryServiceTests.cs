using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
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
        public async Task BuyItemAsync_Throws_WhenInventoryFull()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("tester", "password", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var progression = new Progression(user.Id) { Count = 500 };
            context.Progressions.Add(progression);

            var item = new Item(1, "LimitedItem", 50, 10, 2);
            context.Items.Add(item);

            var existingEntry = new InventoryEntry(user.Id, item.Id, 10);
            context.InventoryEntries.Add(existingEntry);

            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.BuyItemAsync(user.Id, item.Id);
            });
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
        public async Task GetAllItemsAsync_Throws_WhenNoItems()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.GetAllItemsAsync();
            });
        }

        [Fact]
        public async Task GetItemByIdAsync_ReturnsItem_WhenExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Items.Add(new Item(7, "FindMe", 20, 2, 1));
            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var item = await service.GetItemByIdAsync(7);

            Assert.Equal("FindMe", item.Name);
        }

        [Fact]
        public async Task GetItemByIdAsync_Throws_WhenNotFound()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.GetItemByIdAsync(999);
            });
        }

        [Fact]
        public async Task GetUsernameAsync_ReturnsUnknown_WhenUserMissing()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var username = await service.GetUsernameAsync(999);

            Assert.Equal("Unknown", username);
        }

        [Fact]
        public async Task GetUsernameAsync_ReturnsUsername_WhenUserExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("known", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var username = await service.GetUsernameAsync(user.Id);

            Assert.Equal("known", username);
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
        public async Task SeedInventoryAsync_LoadsItemsFromJson()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var originalDir = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var baseDir = AppContext.BaseDirectory;
            var baseItemsPath = Path.Combine(baseDir, "items.json");
            var baseItemsBackup = Path.Combine(baseDir, "items.json.bak");
            try
            {
                if (File.Exists(baseItemsPath))
                {
                    File.Move(baseItemsPath, baseItemsBackup);
                }

                Directory.SetCurrentDirectory(tempDir);
                await File.WriteAllTextAsync(
                    Path.Combine(tempDir, "items.json"),
                    "[{\"id\":1,\"name\":\"A\",\"price\":10,\"maxQuantity\":2,\"clickValue\":1}]"
                );

                await service.SeedInventoryAsync();

                var items = await context.Items.ToListAsync();
                Assert.Single(items);
                Assert.Equal("A", items[0].Name);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
                if (File.Exists(baseItemsBackup))
                {
                    File.Move(baseItemsBackup, baseItemsPath);
                }
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task SeedInventoryAsync_Throws_WhenInvalidJson()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var originalDir = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var baseDir = AppContext.BaseDirectory;
            var baseItemsPath = Path.Combine(baseDir, "items.json");
            var baseItemsBackup = Path.Combine(baseDir, "items.json.bak");
            try
            {
                if (File.Exists(baseItemsPath))
                {
                    File.Move(baseItemsPath, baseItemsBackup);
                }

                Directory.SetCurrentDirectory(tempDir);
                await File.WriteAllTextAsync(Path.Combine(tempDir, "items.json"), "{not-valid-json");

                await Assert.ThrowsAsync<GameException>(async () =>
                {
                    await service.SeedInventoryAsync();
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
                if (File.Exists(baseItemsBackup))
                {
                    File.Move(baseItemsBackup, baseItemsPath);
                }
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task SeedInventoryAsync_Throws_WhenNoValidItems()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var originalDir = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var baseDir = AppContext.BaseDirectory;
            var baseItemsPath = Path.Combine(baseDir, "items.json");
            var baseItemsBackup = Path.Combine(baseDir, "items.json.bak");
            try
            {
                if (File.Exists(baseItemsPath))
                {
                    File.Move(baseItemsPath, baseItemsBackup);
                }

                Directory.SetCurrentDirectory(tempDir);
                await File.WriteAllTextAsync(
                    Path.Combine(tempDir, "items.json"),
                    "[{\"id\":1,\"name\":\" \",\"price\":10,\"maxQuantity\":2,\"clickValue\":1}]"
                );

                await Assert.ThrowsAsync<GameException>(async () =>
                {
                    await service.SeedInventoryAsync();
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
                if (File.Exists(baseItemsBackup))
                {
                    File.Move(baseItemsBackup, baseItemsPath);
                }
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task SeedInventoryAsync_Throws_WhenJsonMissing()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new InventoryService(context, new NullLogger<InventoryService>());

            var originalDir = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var baseDir = AppContext.BaseDirectory;
            var baseItemsPath = Path.Combine(baseDir, "items.json");
            var baseItemsBackup = Path.Combine(baseDir, "items.json.bak");
            try
            {
                if (File.Exists(baseItemsPath))
                {
                    File.Move(baseItemsPath, baseItemsBackup);
                }

                Directory.SetCurrentDirectory(tempDir);

                await Assert.ThrowsAsync<GameException>(async () =>
                {
                    await service.SeedInventoryAsync();
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
                if (File.Exists(baseItemsBackup))
                {
                    File.Move(baseItemsBackup, baseItemsPath);
                }
                Directory.Delete(tempDir, true);
            }
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
    }
}
