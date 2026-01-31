using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using GameServerApi;

namespace GameServerApi.Tests
{
    public class ChatHubTests
    {
        private void ClearConnections()
        {
            var field = typeof(ChatHub).GetField("_connections", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var dict = (ConcurrentDictionary<string, string>?)field!.GetValue(null);
            dict?.Clear();
        }

        private void SetHubContextAndClients(Hub hub, HubCallerContext context, IHubCallerClients clients)
        {
            var clientsProp = typeof(Hub).GetProperty("Clients", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var contextProp = typeof(Hub).GetProperty("Context", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(clientsProp);
            Assert.NotNull(contextProp);
            clientsProp!.SetValue(hub, clients);
            contextProp!.SetValue(hub, context);
        }

        [Fact]
        public async Task OnConnectedAsync_Sends_UpdateUserCount_With_1()
        {
            ClearConnections();

            var hub = new ChatHub(new Mock<Microsoft.Extensions.Logging.ILogger<ChatHub>>().Object);

            var mockClients = new Mock<IHubCallerClients>();
            var mockProxyAll = new Mock<IClientProxy>();
            var mockProxySingle = new Mock<ISingleClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);
            mockClients.Setup(c => c.Others).Returns(mockProxySingle.Object);
            mockClients.Setup(c => c.Caller).Returns(mockProxySingle.Object);

            var mockContext = new Mock<HubCallerContext>();
            var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "mouad1") }, "Test"));
            mockContext.Setup(c => c.ConnectionId).Returns("conn1");
            mockContext.Setup(c => c.User).Returns(claimsPrincipal);

            SetHubContextAndClients(hub, mockContext.Object, mockClients.Object);

            await hub.OnConnectedAsync();

            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "UpdateUserCount",
                    It.Is<object[]>(o => o != null && (int)o[0] == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("mouad1 joined the chat")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("Welcome mouad1! Users online")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_Sends_UpdateUserCount_Decrements()
        {
            ClearConnections();

            // Pre-populate two connections
            var field = typeof(ChatHub).GetField("_connections", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var dict = (ConcurrentDictionary<string, string>?)field!.GetValue(null);
            if (dict != null)
            {
                dict.TryAdd("connA", "connA");
                dict.TryAdd("connB", "connB");
            }

            var hub = new ChatHub(new Mock<Microsoft.Extensions.Logging.ILogger<ChatHub>>().Object);

            var mockClients = new Mock<IHubCallerClients>();
            var mockProxyAll = new Mock<IClientProxy>();
            var mockProxySingle = new Mock<ISingleClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);
            mockClients.Setup(c => c.Others).Returns(mockProxySingle.Object);
            mockClients.Setup(c => c.Caller).Returns(mockProxySingle.Object);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("connA");

            SetHubContextAndClients(hub, mockContext.Object, mockClients.Object);

            await hub.OnDisconnectedAsync(null);

            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "UpdateUserCount",
                    It.Is<object[]>(o => o != null && (int)o[0] == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("connA left the chat")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("Users online")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMessage_Forwards_To_All_Clients()
        {
            var hub = new ChatHub(new Mock<Microsoft.Extensions.Logging.ILogger<ChatHub>>().Object);

            var mockClients = new Mock<IHubCallerClients>();
            var mockProxyAll = new Mock<IClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("connX");

            SetHubContextAndClients(hub, mockContext.Object, mockClients.Object);

            await hub.SendMessage("user1", "hello world");
            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "user1" && (string)o[1] == "hello world"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Register_Sends_SystemMessage_And_UpdateUserCount()
        {
            ClearConnections();

            var hub = new ChatHub(new Mock<Microsoft.Extensions.Logging.ILogger<ChatHub>>().Object);

            var mockClients = new Mock<IHubCallerClients>();
            var mockProxyAll = new Mock<IClientProxy>();
            var mockProxySingle = new Mock<ISingleClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);
            mockClients.Setup(c => c.Others).Returns(mockProxySingle.Object);
            mockClients.Setup(c => c.Caller).Returns(mockProxySingle.Object);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("connReg");

            SetHubContextAndClients(hub, mockContext.Object, mockClients.Object);

            await hub.Register("alice");

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("alice joined the chat")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "UpdateUserCount",
                    It.Is<object[]>(o => o != null && (int)o[0] == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("Welcome alice")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_Sends_LeaveMessage_When_Username_Present()
        {
            ClearConnections();

            // Pre-populate two connections with usernames
            var field = typeof(ChatHub).GetField("_connections", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var dict = (ConcurrentDictionary<string, string>?)field!.GetValue(null);
            if (dict != null)
            {
                dict.TryAdd("connA", "alice");
                dict.TryAdd("connB", "bob");
            }

            var hub = new ChatHub(new Mock<Microsoft.Extensions.Logging.ILogger<ChatHub>>().Object);

            var mockClients = new Mock<IHubCallerClients>();
            var mockProxyAll = new Mock<IClientProxy>();
            var mockProxySingle = new Mock<ISingleClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);
            mockClients.Setup(c => c.Others).Returns(mockProxySingle.Object);
            mockClients.Setup(c => c.Caller).Returns(mockProxySingle.Object);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("connA");

            SetHubContextAndClients(hub, mockContext.Object, mockClients.Object);

            await hub.OnDisconnectedAsync(null);

            mockProxySingle.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "SYSTEM" && ((string)o[1]).Contains("alice left the chat")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "UpdateUserCount",
                    It.Is<object[]>(o => o != null && (int)o[0] == 1),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendMessage_Uses_Registered_Username_When_Using_Overload()
        {
            ClearConnections();

            var hub = new ChatHub(new Mock<Microsoft.Extensions.Logging.ILogger<ChatHub>>().Object);

            var mockClients = new Mock<IHubCallerClients>();
            var mockProxyAll = new Mock<IClientProxy>();
            var mockProxySingle = new Mock<ISingleClientProxy>();
            mockClients.Setup(c => c.All).Returns(mockProxyAll.Object);
            mockClients.Setup(c => c.Others).Returns(mockProxySingle.Object);
            mockClients.Setup(c => c.Caller).Returns(mockProxySingle.Object);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("connY");

            SetHubContextAndClients(hub, mockContext.Object, mockClients.Object);

            await hub.Register("bob");
            await hub.SendMessage(null, "hello everyone");

            mockProxyAll.Verify(
                x => x.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && (string)o[0] == "bob" && (string)o[1] == "hello everyone"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}