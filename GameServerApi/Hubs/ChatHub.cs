using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Security.Claims;

namespace GameServerApi;

public class ChatHub : Hub
{
    private static int _onlinePlayerCount = 0;

    public override async Task OnConnectedAsync()
    {
        _onlinePlayerCount++;
        
        // Get the user ID from the JWT token claims
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Send Login event with the user's ID
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.All.SendAsync("Login", userId);
        }
        
        await Clients.All.SendAsync("UpdateUserCount", _onlinePlayerCount);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _onlinePlayerCount--;
        await Clients.All.SendAsync("UpdateUserCount", _onlinePlayerCount);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
