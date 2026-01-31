using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace GameServerApi;

public class ChatHub : Hub
{
    private static int _onlinePlayerCount = 0;

    public override async Task OnConnectedAsync()
    {
        _onlinePlayerCount++;
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
