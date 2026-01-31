namespace GameServerApi.Services;

using System.Collections.Concurrent;

public class ConnectionTrackerService
{
    private readonly ConcurrentDictionary<int, byte> _onlineUserIds = new();

    public void AddUser(int userId)
    {
        _onlineUserIds.TryAdd(userId, 0);
    }

    public void RemoveUser(int userId)
    {
        _onlineUserIds.TryRemove(userId, out _);
    }

    public bool IsOnline(int userId)
    {
        return _onlineUserIds.ContainsKey(userId);
    }
}
