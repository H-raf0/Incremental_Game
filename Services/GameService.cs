using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;
using GameServerApi.Exceptions;

namespace GameServerApi.Services
{
    public class GameService
    {
        private readonly ApplicationDbContext _context;

        public GameService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Progression> InitializeProgressionAsync(int userId)
        {
            bool exists = await _context.Progressions.AnyAsync(p => p.UserId == userId);
            if (exists)
            {
                throw new GameException("Progression already exists", "PROGRESSION_EXISTS", 400);
            }

            try
            {
                var progression = new Progression(userId);
                _context.Progressions.Add(progression);
                await _context.SaveChangesAsync();
                return progression;
            }
            catch
            {
                throw new GameException("Failed to initialize", "INITIALIZATION_FAILED", 500);
            }
        }

        public async Task<Progression> GetProgressionAsync(int userId)
        {
            var progression = await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();

            if (progression == null)
            {
                throw new GameException("No progressions found", "NO_PROGRESSION", 404);
            }

            return progression;
        }

        public async Task<Progression> ResetProgressionAsync(int userId)
        {
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                throw new GameException("No progression", "NO_PROGRESSION", 404);
            }

            var resetCost = progression.CalculateResetCost();
            if (resetCost > progression.Count)
            {
                throw new GameException("Not enough clicks to reset", "INSUFFICIENT_CLICKS", 400);
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

            return progression;
        }

        public async Task<int> GetResetCostAsync(int userId)
        {
            var progression = await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();

            if (progression == null)
            {
                throw new GameException("No progressions found", "NO_PROGRESSION", 404);
            }

            int cost = progression.CalculateResetCost();
            return cost;
        }

        public async Task<ClickResponse> ClickAsync(int userId)
        {
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                throw new GameException("No progressions found", "NO_PROGRESSION", 404);
            }

            progression.Count += progression.Multiplier + progression.totalClickValue;
            await _context.SaveChangesAsync();

            return new ClickResponse(progression.Count, progression.Multiplier);
        }

        public async Task<BestScoreResponse> GetBestScoreAsync()
        {
            var bestProgression = await _context.Progressions
                .OrderByDescending(p => p.BestScore)
                .FirstOrDefaultAsync();

            if (bestProgression == null || bestProgression.BestScore == 0)
            {
                throw new GameException("No progressions found", "NO_PROGRESSIONS", 404);
            }

            return new BestScoreResponse(bestProgression.UserId, bestProgression.BestScore);
        }
    }
}
