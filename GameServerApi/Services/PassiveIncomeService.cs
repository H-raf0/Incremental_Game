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

            // Send ScoreUpdate event to each connected player
            if (_hubContext == null)
            {
                _logger.LogWarning("PassiveIncomeService: IHubContext<ChatHub> not available, skipping ScoreUpdate sends");
            }
            else
            {
                foreach (var progression in progressions)
                {
                    if (_connectionTrackerService.IsOnline(progression.UserId))
                    {
                        try
                        {
                            var userId = progression.UserId.ToString();
                            await _hubContext.Clients.User(userId).SendAsync("ScoreUpdate", progression.Count);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending ScoreUpdate to user {userId}", progression.UserId);
                        }
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
