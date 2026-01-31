using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;
using GameServerApi.Exceptions;
using GameServerApi;

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

        private sealed class ThrowingDbContext : ApplicationDbContext
        {
            public bool ThrowOnSave { get; set; }
            public bool SaveChangesCalled { get; private set; }

            public ThrowingDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCalled = true;
                if (ThrowOnSave)
                {
                    throw new Exception("boom");
                }
                return base.SaveChangesAsync(cancellationToken);
            }

            public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            {
                SaveChangesCalled = true;
                if (ThrowOnSave)
                {
                    throw new Exception("boom");
                }
                return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
        }

        private static void ResetHighScoreCache(long score = 0, int userId = 0, string username = "")
        {
            var type = typeof(GameService);
            type.GetField("_cachedHighScore", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, score);
            type.GetField("_cachedHighScoreUserId", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, userId);
            type.GetField("_cachedHighScoreUsername", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, username);
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
        public async Task ClickAsync_UpdatesHighScore_ForDifferentUser()
        {
            ResetHighScoreCache();
            var context = CreateContext(Guid.NewGuid().ToString());
            var user1 = new User("u1", "pwd", Role.USER);
            var user2 = new User("u2", "pwd", Role.USER);
            context.Users.AddRange(user1, user2);
            await context.SaveChangesAsync();
            context.Progressions.Add(new Progression(user1.Id));
            context.Progressions.Add(new Progression(user2.Id) { Count = 5 });
            await context.SaveChangesAsync();

            var clientProxyMock = new Mock<IClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("NewHighScore", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock.SetupGet(c => c.All).Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new GameService(context, new NullLogger<GameService>(), hubContextMock.Object);

            var first = await service.ClickAsync(user1.Id);
            var second = await service.ClickAsync(user2.Id);

            Assert.Equal(1, first.Count);
            Assert.Equal(6, second.Count);

            clientProxyMock.Verify(
                c => c.SendCoreAsync("NewHighScore", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
        }

        [Fact]
        public async Task ClickAsync_DoesNotUpdateHighScore_ForSameUser()
        {
            ResetHighScoreCache();
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("u1", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            context.Progressions.Add(new Progression(user.Id));
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            await service.ClickAsync(user.Id);
            var afterFirst = (long)typeof(GameService)
                .GetField("_cachedHighScore", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

            await service.ClickAsync(user.Id);
            var afterSecond = (long)typeof(GameService)
                .GetField("_cachedHighScore", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

            Assert.Equal(afterFirst, afterSecond);
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

            Assert.Equal(100, cost.Cost);
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
        public async Task InitializeProgressionAsync_Throws_WhenSaveFails()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new ThrowingDbContext(options) { ThrowOnSave = true };

            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.InitializeProgressionAsync(123);
            });

            Assert.True(context.SaveChangesCalled);
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
        public async Task ClickAsync_ClampsNegativeNewCountToZero()
        {
            ResetHighScoreCache();
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 17;
            var progression = new Progression(userId) { Count = 0, Multiplier = 0, TotalClickValue = int.MinValue };
            context.Progressions.Add(progression);
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var response = await service.ClickAsync(userId);

            Assert.Equal(0, response.Count);
        }

        [Fact]
        public async Task ClickAsync_UsesUnknownUsername_WhenUserMissing()
        {
            ResetHighScoreCache();
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 18;
            context.Progressions.Add(new Progression(userId));
            await context.SaveChangesAsync();

            var clientProxyMock = new Mock<IClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("NewHighScore", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock.SetupGet(c => c.All).Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new GameService(context, new NullLogger<GameService>(), hubContextMock.Object);

            await service.ClickAsync(userId);

            clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "NewHighScore",
                    It.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "Unknown"),
                    It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ClickAsync_HighScore_WithNullHubContext()
        {
            ResetHighScoreCache();
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("winner", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            context.Progressions.Add(new Progression(user.Id));
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>(), null);

            var response = await service.ClickAsync(user.Id);

            Assert.Equal(1, response.Count);
        }

        [Fact]
        public async Task GetProgressionAsync_ReturnsProgression_WhenExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 15;
            var progression = new Progression(userId) { Count = 42 };
            context.Progressions.Add(progression);
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var result = await service.GetProgressionAsync(userId);

            Assert.Equal(42, result.Count);
            Assert.Equal(userId, result.UserId);
        }

        [Fact]
        public async Task GetProgressionAsync_Throws_WhenNotFound()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.GetProgressionAsync(404);
            });
        }

        [Fact]
        public async Task ClickAsync_CapsAtIntMaxValue()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 16;
            var progression = new Progression(userId) { Count = int.MaxValue, Multiplier = 1, TotalClickValue = 0 };
            context.Progressions.Add(progression);
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var response = await service.ClickAsync(userId);

            Assert.Equal(int.MaxValue, response.Count);
        }

        [Fact]
        public async Task ResetProgressionAsync_Succeeds_UpdatesBestScoreAndClearsInventory()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 20;
            var progression = new Progression(userId) { Count = 500, BestScore = 100, TotalClickValue = 10, Multiplier = 2 };
            context.Progressions.Add(progression);
            context.InventoryEntries.Add(new InventoryEntry(userId, 1, 2));
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var result = await service.ResetProgressionAsync(userId);

            Assert.Equal(500, result.BestScore);
            Assert.Equal(0, result.Count);
            Assert.Equal(0, result.TotalClickValue);
            Assert.Equal(3, result.Multiplier);

            var inventoryCount = await context.InventoryEntries.CountAsync(i => i.UserId == userId);
            Assert.Equal(0, inventoryCount);
        }

        [Fact]
        public async Task ResetProgressionAsync_SendsPlayerReset_WhenHubProvided()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("resetUser", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            context.Progressions.Add(new Progression(user.Id) { Count = 200, Multiplier = 2 });
            await context.SaveChangesAsync();

            var clientProxyMock = new Mock<IClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("PlayerReset", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock.SetupGet(c => c.All).Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new GameService(context, new NullLogger<GameService>(), hubContextMock.Object);

            await service.ResetProgressionAsync(user.Id);

            clientProxyMock.Verify(
                c => c.SendCoreAsync("PlayerReset", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ResetProgressionAsync_UsesUnknownUsername_WhenUserMissing()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 55;
            context.Progressions.Add(new Progression(userId) { Count = 200, Multiplier = 2 });
            await context.SaveChangesAsync();

            var clientProxyMock = new Mock<IClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("PlayerReset", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock.SetupGet(c => c.All).Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new GameService(context, new NullLogger<GameService>(), hubContextMock.Object);

            await service.ResetProgressionAsync(userId);

            clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "PlayerReset",
                    It.Is<object?[]>(args => args.Length == 2 && (string)args[0]! == "Unknown"),
                    It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ResetProgressionAsync_DoesNotUpdateBestScore_WhenAlreadyHigher()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 56;
            var progression = new Progression(userId) { Count = 150, BestScore = 200, Multiplier = 1 };
            context.Progressions.Add(progression);
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            var result = await service.ResetProgressionAsync(userId);

            Assert.Equal(200, result.BestScore);
        }

        [Fact]
        public async Task ResetProgressionAsync_Logs_WhenLoggerEnabled()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var userId = 57;
            context.Progressions.Add(new Progression(userId) { Count = 200, Multiplier = 2 });
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<GameService>>();
            loggerMock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            loggerMock.Setup(l => l.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
                .Verifiable();

            var service = new GameService(context, loggerMock.Object);

            await service.ResetProgressionAsync(userId);

            loggerMock.Verify();
        }

        [Fact]
        public async Task ResetProgressionAsync_Throws_WhenNoProgression()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.ResetProgressionAsync(999);
            });
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
        public async Task GetResetCostAsync_Throws_WhenNoProgression()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.GetResetCostAsync(123);
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
        public async Task GetBestScoreAsync_Throws_WhenBestScoreZero()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(40) { BestScore = 0 });
            await context.SaveChangesAsync();

            var service = new GameService(context, new NullLogger<GameService>());

            await Assert.ThrowsAsync<GameException>(async () =>
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
