using System;
using System.Threading;
using System.Threading.Tasks;
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

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>());

            await service.DistributePassiveIncomeAsync();

            var p1 = await context.Progressions.FirstAsync(p => p.UserId == 1);
            var p2 = await context.Progressions.FirstAsync(p => p.UserId == 2);
            Assert.Equal(6, p1.Count);
            Assert.Equal(11, p2.Count);
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_SendsScoreUpdate_WhenHubProvided()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 1 });
            context.Progressions.Add(new Progression(2) { Count = 2 });
            await context.SaveChangesAsync();

            var clientProxyMock = new Mock<IClientProxy>();
            clientProxyMock
                .Setup(c => c.SendCoreAsync("ScoreUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.User(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), hubContextMock.Object);

            await service.DistributePassiveIncomeAsync();

            hubClientsMock.Verify(c => c.User("1"), Times.Once);
            hubClientsMock.Verify(c => c.User("2"), Times.Once);
            clientProxyMock.Verify(
                c => c.SendCoreAsync("ScoreUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
        }

        [Fact]
        public async Task DistributePassiveIncomeAsync_NoUsers_NoHubCalls()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            var clientProxyMock = new Mock<IClientProxy>();
            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .Setup(c => c.User(It.IsAny<string>()))
                .Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var service = new PassiveIncomeService(context, new NullLogger<PassiveIncomeService>(), hubContextMock.Object);

            await service.DistributePassiveIncomeAsync();

            clientProxyMock.Verify(
                c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        }
    }
}
