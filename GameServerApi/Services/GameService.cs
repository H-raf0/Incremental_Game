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

        // Cache du high score
        private static long _cachedHighScore = 0;
        private static int _cachedHighScoreUserId = 0;
        private static string _cachedHighScoreUsername = "";

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
            progression.TotalClickValue = 0;
            progression.Multiplier++;

            var inventoryEntries = _context.InventoryEntries.Where(i => i.UserId == userId);
            _context.InventoryEntries.RemoveRange(inventoryEntries);

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Progression reset successfully: UserId {UserId}, NewMultiplier: {Multiplier}", userId, progression.Multiplier);

            // Try to fetch the user's username to include in the system message
            var user = await _context.Users.FindAsync(userId);
            var username = user?.Username ?? "Unknown";

            // Send a PlayerReset event to the chat hub if hub context is available
            if (_hubContext != null)
            {
                // Notify all clients that a player has reset: provide player name and the previous score
                await _hubContext.Clients.All.SendAsync("PlayerReset", username, previousCount);
            }

            return progression;
        }

        public async Task<ResetCostResponse> GetResetCostAsync(int userId)
        {
            var progression = await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();

            if (progression == null)
            {
                throw new GameException("No progressions found", "NO_PROGRESSION", 404);
            }

            int cost = progression.CalculateResetCost();
            return new ResetCostResponse(cost);
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

            progression.Count += progression.Multiplier + progression.TotalClickValue;
            await _context.SaveChangesAsync();

            // Vérifier si c'est un nouveau record d'un AUTRE utilisateur (pas de spam du même user)
            if (progression.Count > _cachedHighScore && userId != _cachedHighScoreUserId)
            {
                _cachedHighScore = progression.Count;
                _cachedHighScoreUserId = userId;

                // Récupérer le username
                var user = await _context.Users.FindAsync(userId);
                _cachedHighScoreUsername = user?.Username ?? "Unknown";

                _logger.LogInformation("New High Score! UserId {UserId}, Username: {Username}, Score: {Score}", userId, _cachedHighScoreUsername, _cachedHighScore);

                // Envoyer la notification à tous les clients
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("NewHighScore", _cachedHighScoreUsername, _cachedHighScore);
                }
            }

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
