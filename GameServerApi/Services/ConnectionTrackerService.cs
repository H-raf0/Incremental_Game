namespace GameServerApi.Services;

using System.Collections.Concurrent;

public class ConnectionTrackerService
{
    private static int _onlinePlayerCount = 0;
    public static int OnlineUserCount => _onlinePlayerCount;

    // userId -> set of connectionIds
    private readonly ConcurrentDictionary<int, HashSet<string>> _userConnections = new();
    // connectionId -> userId
    private readonly ConcurrentDictionary<string, int> _connectionToUser = new();

    public void AddConnection(int userId, string connectionId)
    {
        _userConnections.AddOrUpdate(userId,
            _ => new HashSet<string> { connectionId },
            (_, set) => { lock (set) { set.Add(connectionId); } return set; });
        _connectionToUser[connectionId] = userId;
        _onlinePlayerCount = _userConnections.Count;
    }

    public void RemoveConnection(string connectionId)
    {
        if (_connectionToUser.TryRemove(connectionId, out int userId))
        {
            if (_userConnections.TryGetValue(userId, out var set))
            {
                lock (set)
                {
                    set.Remove(connectionId);
                    if (set.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                    }
                }
            }
            _onlinePlayerCount = _userConnections.Count;
        }
    }

    public IEnumerable<string> GetConnections(int userId)
    {
        if (_userConnections.TryGetValue(userId, out var set))
        {
            lock (set)
            {
                return set.ToList();
            }
        }
        return Enumerable.Empty<string>();
    }

    public bool IsOnline(int userId)
    {
        return _userConnections.ContainsKey(userId);
    }
}
