using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Security.Claims;
using GameServerApi.Services;

namespace GameServerApi;


public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly ConnectionTrackerService _connectionTrackerService;

    public ChatHub(ILogger<ChatHub> logger, ConnectionTrackerService connectionTrackerService)
    {
        _logger = logger;
        _connectionTrackerService = connectionTrackerService;
    }



    /// <summary>
    /// Handles client connection to the chat hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        int count = ConnectionTrackerService.OnlineUserCount;
        _logger.LogInformation("ChatHub: Client connected. Total online users: {count}", count);
        await Clients.All.SendAsync("UpdateUserCount", count);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handles client disconnection from the chat hub.
    /// </summary>
    /// <param name="exception">Any exception that caused the disconnection.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Remove this connection from tracker
        _connectionTrackerService.RemoveConnection(Context.ConnectionId);
        int count = ConnectionTrackerService.OnlineUserCount;
        _logger.LogInformation("ChatHub: Client disconnected. Total online users: {count}", count);
        try
        {
            await Clients.All.SendAsync("UpdateUserCount", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending UpdateUserCount");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Logs in a user for the current connection.
    /// </summary>
    /// <param name="userId">The ID of the user logging in.</param>
    public async Task Login(int userId)
    {
        _logger.LogInformation("ChatHub.Login called with userId={userId}, connectionId={connectionId}", userId, Context.ConnectionId);
        _connectionTrackerService.AddConnection(userId, Context.ConnectionId);
        int count = ConnectionTrackerService.OnlineUserCount;
        var connections = _connectionTrackerService.GetConnections(userId);
        _logger.LogInformation("ChatHub: User {userId} now has {connectionCount} connection(s): [{connections}]", userId, connections.Count(), string.Join(",", connections));
        _logger.LogInformation("ChatHub: User {userId} logged in with connection {connectionId}. Online users: {count}", userId, Context.ConnectionId, count);
        await Clients.All.SendAsync("UpdateUserCount", count);
    }

    /// <summary>
    /// Broadcasts a message to all connected clients.
    /// </summary>
    /// <param name="user">The username of the sender.</param>
    /// <param name="message">The message content.</param>
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
