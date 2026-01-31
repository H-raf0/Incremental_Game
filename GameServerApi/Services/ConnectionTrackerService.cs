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

    /// <summary>
    /// Adds a new connection for a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="connectionId">The connection ID from SignalR.</param>
    public void AddConnection(int userId, string connectionId)
    {
        _userConnections.AddOrUpdate(userId,
            _ => new HashSet<string> { connectionId },
            (_, set) => { lock (set) { set.Add(connectionId); } return set; });
        _connectionToUser[connectionId] = userId;
        _onlinePlayerCount = _userConnections.Count;
    }

    /// <summary>
    /// Removes a connection for a user.
    /// </summary>
    /// <param name="connectionId">The connection ID to remove.</param>
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

    /// <summary>
    /// Retrieves all connection IDs for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>An enumerable of connection IDs.</returns>
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

    /// <summary>
    /// Checks if a user is currently online.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user has active connections, false otherwise.</returns>
    public bool IsOnline(int userId)
    {
        return _userConnections.ContainsKey(userId);
    }
}
