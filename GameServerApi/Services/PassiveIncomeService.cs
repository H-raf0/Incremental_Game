namespace GameServerApi.Services;

using GameServerApi.Models;
using Microsoft.EntityFrameworkCore;

public class PassiveIncomeService
{
    private readonly ApplicationDbContext _context;

    public PassiveIncomeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ApplyPassiveIncomeAsync(int userId)
    {
        var income = await _context.PassiveIncomes.FirstOrDefaultAsync(p => p.UserId == userId);
        if (income == null) return;

        // Calculate time passed since last calculation
        var timePassed = (DateTime.UtcNow - income.LastCalculatedAt).TotalSeconds;
        // Calculate amount gained based on income per second
        var gainedAmount = (decimal)(timePassed * (double)income.IncomePerSecond);

        // Add the gained amount to the player's score
        var progression = await _context.Progressions.FirstOrDefaultAsync(p => p.UserId == userId);
        if (progression != null)
        {
            progression.Score += gainedAmount;
            income.LastCalculatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task InitializePassiveIncomeAsync(int userId, decimal initialIncome = 0.1m)
    {
        var existingIncome = await _context.PassiveIncomes.FirstOrDefaultAsync(p => p.UserId == userId);
        if (existingIncome == null)
        {
            _context.PassiveIncomes.Add(new PassiveIncome
            {
                UserId = userId,
                IncomePerSecond = initialIncome,
                LastCalculatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateIncomePerSecondAsync(int userId, decimal newIncome)
    {
        var income = await _context.PassiveIncomes.FirstOrDefaultAsync(p => p.UserId == userId);
        if (income != null)
        {
            income.IncomePerSecond = newIncome;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PassiveIncome?> GetPassiveIncomeAsync(int userId)
    {
        return await _context.PassiveIncomes.FirstOrDefaultAsync(p => p.UserId == userId);
    }
}
