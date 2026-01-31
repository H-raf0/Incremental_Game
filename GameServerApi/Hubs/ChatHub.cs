using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Security.Claims;
using GameServerApi.Services;

namespace GameServerApi;

public class ChatHub : Hub
{
    private static int _onlinePlayerCount = 0;
    private readonly ILogger<ChatHub> _logger;
    private readonly ConnectionTrackerService _connectionTrackerService;

    public ChatHub(ILogger<ChatHub> logger, ConnectionTrackerService connectionTrackerService)
    {
        _logger = logger;
        _connectionTrackerService = connectionTrackerService;
    }

    public override async Task OnConnectedAsync()
    {
        _onlinePlayerCount++;
        
        // Get the user ID from the JWT token claims
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Track user connection
        if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var uid))
        {
            _connectionTrackerService.AddUser(uid);
        }
        
        // Send Login event with the user's ID
        _logger.LogInformation("ChatHub: OnConnectedAsync connectionId={connectionId} userId={userId}", Context.ConnectionId, userId);
        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                await Clients.All.SendAsync("Login", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Login event to clients for user {userId}", userId);
            }
        }
        
        await Clients.All.SendAsync("UpdateUserCount", _onlinePlayerCount);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _onlinePlayerCount--;
        
        // Remove user from tracking
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var uid))
        {
            _connectionTrackerService.RemoveUser(uid);
        }
        
        _logger.LogInformation("ChatHub: OnDisconnectedAsync connectionId={connectionId} userId={userId} exception={exception}", Context.ConnectionId, userId, exception?.Message);
        try
        {
            await Clients.All.SendAsync("UpdateUserCount", _onlinePlayerCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending UpdateUserCount");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
