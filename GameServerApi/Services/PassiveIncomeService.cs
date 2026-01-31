namespace GameServerApi.Services;

using GameServerApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

public class PassiveIncomeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PassiveIncomeService> _logger;
    private readonly IHubContext<ChatHub>? _hubContext;
    private readonly ConnectionTrackerService _connectionTrackerService;

    public PassiveIncomeService(ApplicationDbContext context, ILogger<PassiveIncomeService> logger, ConnectionTrackerService connectionTrackerService, IHubContext<ChatHub>? hubContext = null)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
        _connectionTrackerService = connectionTrackerService;
    }

    // Distribute 1 point to all users' score
    public async Task DistributePassiveIncomeAsync()
    {
        try
        {
            var progressions = await _context.Progressions.ToListAsync();
            
            if (progressions.Count == 0)
            {
                _logger.LogInformation("No users to distribute passive income to");
                return;
            }

            // Add 1 point to each user
            foreach (var progression in progressions)
            {
                progression.Count += 1;
            }

            await _context.SaveChangesAsync();

            // Send ScoreUpdate event to each connected player (only to the concerned user)
            if (_hubContext == null)
            {
                _logger.LogWarning("PassiveIncomeService: IHubContext<ChatHub> not available, skipping ScoreUpdate sends");
            }
            else
            {
                foreach (var progression in progressions)
                {
                    var userId = progression.UserId;
                    bool isOnline = _connectionTrackerService.IsOnline(userId);
                    _logger.LogInformation("PassiveIncomeService: User {userId} online status: {isOnline}", userId, isOnline);
                    if (isOnline)
                    {
                        var connections = _connectionTrackerService.GetConnections(userId);
                        if (!connections.Any())
                        {
                            _logger.LogWarning("PassiveIncomeService: User {userId} is marked online but has no active connections", userId);
                        }
                        foreach (var connectionId in connections)
                        {
                            try
                            {
                                await _hubContext.Clients.Client(connectionId).SendAsync("ScoreUpdate", progression.Count);
                                _logger.LogInformation("PassiveIncomeService: Sent ScoreUpdate to user {userId} (connection {connectionId}) with count {count}", userId, connectionId, progression.Count);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending ScoreUpdate to user {userId} (connection {connectionId})", userId, connectionId);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("PassiveIncomeService: User {userId} is offline, not sending ScoreUpdate", userId);
                    }
                }
            }

            _logger.LogInformation($"Passive income distributed: +1 point to {progressions.Count} user(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error distributing passive income");
        }
    }
}
