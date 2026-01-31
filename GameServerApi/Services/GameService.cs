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

        // Cache of the high score
        private static long _cachedHighScore = 0;
        private static int _cachedHighScoreUserId = 0;
        private static string _cachedHighScoreUsername = "";

        public GameService(ApplicationDbContext context, ILogger<GameService> logger, IHubContext<ChatHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Initializes a new game progression for a user.
        /// </summary>
        /// <param name="userId">The ID of the user to initialize progression for.</param>
        /// <returns>The newly created progression object.</returns>
        /// <exception cref="GameException">Thrown if progression already exists or initialization fails.</exception>
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

        /// <summary>
        /// Retrieves the game progression for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The user's progression object.</returns>
        /// <exception cref="GameException">Thrown if no progression is found for the user.</exception>
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

        /// <summary>
        /// Resets the game progression for a user, incrementing their multiplier and clearing items.
        /// </summary>
        /// <param name="userId">The ID of the user whose progression should be reset.</param>
        /// <returns>The reset progression object.</returns>
        /// <exception cref="GameException">Thrown if no progression exists or insufficient clicks to reset.</exception>
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

        /// <summary>
        /// Calculates and returns the cost to reset the user's progression.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A response containing the reset cost.</returns>
        /// <exception cref="GameException">Thrown if no progression is found for the user.</exception>
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

        /// <summary>
        /// Processes a click action for a user, updating their score and checking for high score records.
        /// </summary>
        /// <param name="userId">The ID of the user clicking.</param>
        /// <returns>A response containing the updated count and multiplier.</returns>
        /// <exception cref="GameException">Thrown if no progression is found for the user.</exception>
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

            // Calculate the new count, prevent overflow, and ensure it never becomes negative
            long newCount = (long)progression.Count + progression.Multiplier + progression.TotalClickValue;
            if (newCount > int.MaxValue)
            {
                progression.Count = int.MaxValue;
            }
            else
            {
                progression.Count = (int)Math.Max(0, newCount);
            }
            await _context.SaveChangesAsync();

            // Check if this is a new high score from a DIFFERENT user (prevents spam from same user)
            if (progression.Count > _cachedHighScore && userId != _cachedHighScoreUserId)
            {
                _cachedHighScore = progression.Count;
                _cachedHighScoreUserId = userId;

                // Retrieve the username
                var user = await _context.Users.FindAsync(userId);
                _cachedHighScoreUsername = user?.Username ?? "Unknown";

                _logger.LogInformation("New High Score! UserId {UserId}, Username: {Username}, Score: {Score}", userId, _cachedHighScoreUsername, _cachedHighScore);

                // Notify all clients
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("NewHighScore", _cachedHighScoreUsername, _cachedHighScore);
                }
            }

            _logger.LogDebug("Click successful: UserId {UserId}, NewCount: {Count}", userId, progression.Count);
            return new ClickResponse(progression.Count, progression.Multiplier);
        }

        /// <summary>
        /// Retrieves the best score achieved by any user.
        /// </summary>
        /// <returns>A response containing the user ID and best score.</returns>
        /// <exception cref="GameException">Thrown if no progressions exist.</exception>
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
