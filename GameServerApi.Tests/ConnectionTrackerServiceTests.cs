using System;
using Xunit;
using GameServerApi.Services;

namespace GameServerApi.Tests
{
    [CollectionDefinition("ConnectionTracker", DisableParallelization = true)]
    public class ConnectionTrackerCollectionDefinition { }

    [Collection("ConnectionTracker")]
    public class ConnectionTrackerServiceTests
    {
        [Fact]
        public void AddConnection_AddsAndRemovesConnections()
        {
            var service = new ConnectionTrackerService();
            var userId = 1;
            var connectionId = Guid.NewGuid().ToString("N");

            service.AddConnection(userId, connectionId);

            Assert.True(service.IsOnline(userId));
            Assert.Contains(connectionId, service.GetConnections(userId));
            Assert.Equal(1, ConnectionTrackerService.OnlineUserCount);

            service.RemoveConnection(connectionId);

            Assert.False(service.IsOnline(userId));
            Assert.Empty(service.GetConnections(userId));
            Assert.Equal(0, ConnectionTrackerService.OnlineUserCount);
        }

        [Fact]
        public void AddConnection_MultipleConnectionsSameUser()
        {
            var service = new ConnectionTrackerService();
            var userId = 2;
            var c1 = Guid.NewGuid().ToString("N");
            var c2 = Guid.NewGuid().ToString("N");

            service.AddConnection(userId, c1);
            service.AddConnection(userId, c2);

            var connections = service.GetConnections(userId);
            Assert.Contains(c1, connections);
            Assert.Contains(c2, connections);
            Assert.Equal(1, ConnectionTrackerService.OnlineUserCount);

            service.RemoveConnection(c1);
            service.RemoveConnection(c2);

            Assert.False(service.IsOnline(userId));
            Assert.Empty(service.GetConnections(userId));
            Assert.Equal(0, ConnectionTrackerService.OnlineUserCount);
        }

        [Fact]
        public void RemoveConnection_UnknownConnection_DoesNotThrow()
        {
            var service = new ConnectionTrackerService();

            service.RemoveConnection("missing");

            Assert.Equal(0, ConnectionTrackerService.OnlineUserCount);
        }
    }
}
