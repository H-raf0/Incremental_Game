namespace GameServerApi.Services;

using GameServerApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

public class PassiveIncomeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PassiveIncomeService> _logger;
    private readonly IHubContext<ChatHub>? _hubContext;

    public PassiveIncomeService(ApplicationDbContext context, ILogger<PassiveIncomeService> logger, IHubContext<ChatHub>? hubContext = null)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
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
            if (_hubContext != null)
            {
                foreach (var progression in progressions)
                {
                    // Send score update only to the specific player via their user ID
                    await _hubContext.Clients.User(progression.UserId.ToString()).SendAsync("ScoreUpdate", progression.Count);
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
