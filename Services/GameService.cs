using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;

namespace GameServerApi.Services
{
    public class GameService
    {
        private readonly ApplicationDbContext _context;

        public GameService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, Progression? Progression, ErrorResponse? Error)> InitializeProgressionAsync(int userId)
        {
            bool exists = await _context.Progressions.AnyAsync(p => p.UserId == userId);
            if (exists)
            {
                return (false, null, new ErrorResponse("Progression already exists", "PROGRESSION_EXISTS"));
            }

            try
            {
                var progression = new Progression(userId);
                _context.Progressions.Add(progression);
                await _context.SaveChangesAsync();
                return (true, progression, null);
            }
            catch
            {
                return (false, null, new ErrorResponse("Failed to initialize", "INITIALIZATION_FAILED"));
            }
        }

        public async Task<Progression?> GetProgressionAsync(int userId)
        {
            return await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task<(bool Success, Progression? Progression, ErrorResponse? Error)> ResetProgressionAsync(int userId)
        {
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                return (false, null, new ErrorResponse("No progression", "NO_PROGRESSION"));
            }

            var resetCost = progression.CalculateResetCost();
            if (resetCost > progression.Count)
            {
                return (false, null, new ErrorResponse("Not enough clicks to reset", "INSUFFICIENT_CLICKS"));
            }

            if (progression.Count > progression.BestScore)
            {
                progression.BestScore = progression.Count;
            }

            progression.Count = 0;
            progression.totalClickValue = 0;
            progression.Multiplier++;

            var inventoryEntries = _context.InventoryEntries.Where(i => i.UserId == userId);
            _context.InventoryEntries.RemoveRange(inventoryEntries);

            await _context.SaveChangesAsync();

            return (true, progression, null);
        }

        public async Task<(bool Success, int Cost, ErrorResponse? Error)> GetResetCostAsync(int userId)
        {
            var progression = await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();

            if (progression == null)
            {
                return (false, 0, new ErrorResponse("No progressions found", "NO_PROGRESSION"));
            }

            int cost = progression.CalculateResetCost();
            return (true, cost, null);
        }

        public async Task<(bool Success, ClickResponse? Response, ErrorResponse? Error)> ClickAsync(int userId)
        {
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                return (false, null, new ErrorResponse("No progressions found", "NO_PROGRESSION"));
            }

            progression.Count += progression.Multiplier + progression.totalClickValue;
            await _context.SaveChangesAsync();

            return (true, new ClickResponse(progression.Count, progression.Multiplier), null);
        }

        public async Task<BestScoreResponse?> GetBestScoreAsync()
        {
            var bestProgression = await _context.Progressions
                .OrderByDescending(p => p.BestScore)
                .FirstOrDefaultAsync();

            if (bestProgression == null || bestProgression.BestScore == 0)
            {
                return null;
            }

            return new BestScoreResponse(bestProgression.UserId, bestProgression.BestScore);
        }
    }
}
