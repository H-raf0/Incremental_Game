using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;
using GameServerApi;

namespace GameServerApi.Tests
{
    public class PassiveIncomeServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_IncrementsCounts()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 5 });
            context.Progressions.Add(new Progression(2) { Count = 10 });
            await context.SaveChangesAsync();

            var connectionTracker = new ConnectionTrackerService();
            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker);

            await service.DistributePassiveIncomeAsync();

            var p1 = await context.Progressions.FirstAsync(p => p.UserId == 1);
            var p2 = await context.Progressions.FirstAsync(p => p.UserId == 2);
            Assert.Equal(6, p1.Count);
            Assert.Equal(11, p2.Count);
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_HubNull_DoesNotThrow()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 1 });
            await context.SaveChangesAsync();

            var connectionTracker = new ConnectionTrackerService();
            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker, null);

            await service.DistributePassiveIncomeAsync();

            var p1 = await context.Progressions.FirstAsync(p => p.UserId == 1);
            Assert.Equal(2, p1.Count);
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_SendsScoreUpdate_WhenHubProvided()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 1 });
            context.Progressions.Add(new Progression(2) { Count = 2 });
            await context.SaveChangesAsync();

            var connectionTracker = new ConnectionTrackerService();
            connectionTracker.AddConnection(1, "c1");
            connectionTracker.AddConnection(2, "c2");

            var clientProxyMock = new Mock<ISingleClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("ScoreUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.Client(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker, hubContextMock.Object);

            try
            {
                await service.DistributePassiveIncomeAsync();

                hubClientsMock.Verify(c => c.Client("c1"), Times.Once);
                hubClientsMock.Verify(c => c.Client("c2"), Times.Once);
                clientProxyMock.Verify(
                    c => c.SendCoreAsync("ScoreUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(2)
                );
            }
            finally
            {
                connectionTracker.RemoveConnection("c1");
                connectionTracker.RemoveConnection("c2");
            }
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_OfflineUser_NoSend()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 1 });
            await context.SaveChangesAsync();

            var connectionTracker = new ConnectionTrackerService();

            var clientProxyMock = new Mock<ISingleClientProxy>();
            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.Client(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker, hubContextMock.Object);

            await service.DistributePassiveIncomeAsync();

            hubClientsMock.Verify(c => c.Client(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_OnlineWithNoConnections_NoSend()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 1 });
            await context.SaveChangesAsync();

            var connectionTracker = new ConnectionTrackerService();
            var userConnectionsField = typeof(ConnectionTrackerService)
                .GetField("_userConnections", BindingFlags.NonPublic | BindingFlags.Instance);
            var map = (ConcurrentDictionary<int, HashSet<string>>)userConnectionsField!.GetValue(connectionTracker)!;
            map[1] = new HashSet<string>();

            var clientProxyMock = new Mock<ISingleClientProxy>();
            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.Client(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker, hubContextMock.Object);

            await service.DistributePassiveIncomeAsync();

            hubClientsMock.Verify(c => c.Client(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_SendThrows_IsHandled()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 1 });
            await context.SaveChangesAsync();

            var connectionTracker = new ConnectionTrackerService();
            connectionTracker.AddConnection(1, "c1");

            var clientProxyMock = new Mock<ISingleClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("ScoreUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("boom"));

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.Client(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker, hubContextMock.Object);

            try
            {
                await service.DistributePassiveIncomeAsync();
            }
            finally
            {
                connectionTracker.RemoveConnection("c1");
            }
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_NoUsers_NoHubCalls()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            var clientProxyMock = new Mock<ISingleClientProxy>();
            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.Client(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var connectionTracker = new ConnectionTrackerService();
            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), connectionTracker, hubContextMock.Object);

            await service.DistributePassiveIncomeAsync();

            clientProxyMock.Verify(
                c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }
    }
}
