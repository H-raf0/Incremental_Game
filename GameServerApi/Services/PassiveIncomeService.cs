namespace GameServerApi.Services;

using GameServerApi.Models;
using Microsoft.EntityFrameworkCore;

public class PassiveIncomeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PassiveIncomeService> _logger;

    public PassiveIncomeService(ApplicationDbContext context, ILogger<PassiveIncomeService> logger)
    {
        _context = context;
        _logger = logger;
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
            _logger.LogInformation($"Passive income distributed: +1 point to {progressions.Count} user(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error distributing passive income");
        }
    }
}
