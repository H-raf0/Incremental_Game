using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;
using GameServerApi.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace GameServerApi.Services
{
    public class GameService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GameService> _logger;
        private readonly IHubContext<ChatHub>? _hubContext;

        public GameService(ApplicationDbContext context, ILogger<GameService> logger, IHubContext<ChatHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
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
                _logger.LogError("Failed to initialize progression for UserId {UserId}", userId);
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
            _logger.LogInformation("Progression reset attempt: UserId {UserId}", userId);
            
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                _logger.LogWarning("Progression reset failed: No progression found - UserId {UserId}", userId);
                throw new GameException("No progression", "NO_PROGRESSION", 404);
            }

            var resetCost = progression.CalculateResetCost();
            if (resetCost > progression.Count)
            {
                _logger.LogWarning("Progression reset failed: Insufficient clicks - UserId {UserId}, Available: {Available}, Required: {Required}", userId, progression.Count, resetCost);
                throw new GameException("Not enough clicks to reset", "INSUFFICIENT_CLICKS", 400);
            }

            // Capture previous score for the system message
            var previousCount = progression.Count;

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
            
            _logger.LogInformation("Progression reset successfully: UserId {UserId}, NewMultiplier: {Multiplier}", userId, progression.Multiplier);

            // Try to fetch the user's username to include in the event
            var user = await _context.Users.FindAsync(userId);
            var username = user?.Username ?? "Unknown";

            // Broadcast PlayerReset event with username and new score to the chat hub if hub context is available
            if (_hubContext != null)
            {
                // progression.Count is the new score (0)
                await _hubContext.Clients.All.SendAsync("PlayerReset", username, progression.Count);

                // Also send a legacy system message so older clients that listen to ReceiveMessage still see the reset announcement
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "SYSTEM", $"{username} reseted his score of {previousCount} points !");
            }

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
            _logger.LogDebug("Click event: UserId {UserId}", userId);
            
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                _logger.LogWarning("Click failed: No progression found - UserId {UserId}", userId);
                throw new GameException("No progressions found", "NO_PROGRESSION", 404);
            }

            // Use a larger intermediate type to avoid integer overflow and clamp the value to int.MaxValue
            long increment = (long)progression.Multiplier + (long)progression.totalClickValue;
            long newCountLong = (long)progression.Count + increment;
            if (newCountLong > int.MaxValue)
            {
                progression.Count = int.MaxValue;
            }
            else
            {
                progression.Count = (int)newCountLong;
            }

            // Ensure non-negative
            if (progression.Count < 0) progression.Count = 0;

            await _context.SaveChangesAsync();

            _logger.LogDebug("Click successful: UserId {UserId}, NewCount: {Count}", userId, progression.Count);
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
