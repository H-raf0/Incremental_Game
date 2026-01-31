using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GameServerApi;

public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }
    // Thread-safe collection to keep track of connected clients and their usernames
    private static readonly ConcurrentDictionary<string, string?> _connections = new();

    public override async Task OnConnectedAsync()
    {
        // Add connection with no username until the client registers
        _connections.TryAdd(Context.ConnectionId, null);
        _logger.LogInformation("ChatHub: OnConnectedAsync - ConnectionId {ConnectionId}, total {Count}", Context.ConnectionId, _connections.Count);

        // Try to get username from authentication claims if present
        var claimName = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrEmpty(claimName))
        {
            _connections.AddOrUpdate(Context.ConnectionId, claimName, (k, v) => claimName);
            // Broadcast the updated user count to all clients
            await Clients.All.SendAsync("UpdateUserCount", _connections.Count);
            // Announce to others using the username
            await Clients.Others.SendAsync("ReceiveMessage", "SYSTEM", $"{claimName} joined the chat");
            await Clients.Caller.SendAsync("ReceiveMessage", "SYSTEM", $"Welcome {claimName}! Users online: {_connections.Count}");
        }
        else
        {
            // No username known yet - broadcast generic count and messages
            await Clients.All.SendAsync("UpdateUserCount", _connections.Count);
            // Small delay to increase chance that the connecting client is ready to receive messages
            await Task.Delay(50);
            await Clients.Others.SendAsync("ReceiveMessage", "SYSTEM", $"A user connected. Users online: {_connections.Count}");
            await Clients.Caller.SendAsync("ReceiveMessage", "SYSTEM", $"Welcome! Users online: {_connections.Count}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Remove connection and get username (if any)
        _connections.TryRemove(Context.ConnectionId, out var username);
        _logger.LogInformation("ChatHub: OnDisconnectedAsync - ConnectionId {ConnectionId}, username {Username}, total {Count}", Context.ConnectionId, username, _connections.Count);

        // If we had a username registered, announce departure to others
        if (!string.IsNullOrEmpty(username))
        {
            await Clients.Others.SendAsync("ReceiveMessage", "SYSTEM", $"{username} left the chat");
        }
        else
        {
            // Generic leave message for clients that don't have username
            await Clients.Others.SendAsync("ReceiveMessage", "SYSTEM", "A user disconnected from the chat");
        }

        // Broadcast the updated user count to all clients
        await Clients.All.SendAsync("UpdateUserCount", _connections.Count);

        // Also announce the current count to other clients and send a final notice to the caller
        await Task.Delay(50);
        await Clients.Others.SendAsync("ReceiveMessage", "SYSTEM", $"Users online: {_connections.Count}");

        await base.OnDisconnectedAsync(exception);
    }

    // Client calls this after connecting to set their username
    public async Task Register(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        _connections.AddOrUpdate(Context.ConnectionId, username, (k, v) => username);
        _logger.LogInformation("ChatHub: Register - ConnectionId {ConnectionId} registered as {Username}", Context.ConnectionId, username);

        // Announce to others that this user joined and update counts
        await Clients.Others.SendAsync("ReceiveMessage", "SYSTEM", $"{username} joined the chat");
        await Clients.All.SendAsync("UpdateUserCount", _connections.Count);

        // Send welcome to caller
        await Clients.Caller.SendAsync("ReceiveMessage", "SYSTEM", $"Welcome {username}! Users online: {_connections.Count}");
    }

    // SendMessage accepts an optional user; if null/empty, uses the registered username or Anonymous
    public async Task SendMessage(string? user, string message)
    {
        string sender = user ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sender))
        {
            _connections.TryGetValue(Context.ConnectionId, out var username);
            sender = string.IsNullOrEmpty(username) ? "Anonymous" : username;
        }

        _logger.LogInformation("ChatHub: SendMessage - {Sender}: {Message}", sender, message);
        await Clients.All.SendAsync("ReceiveMessage", sender, message);
    }
}
