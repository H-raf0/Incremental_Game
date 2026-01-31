using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using GameServerApi.Models;
using System.Numerics;
using GameServerApi.Exceptions;
using Microsoft.AspNetCore.RateLimiting;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GameController : ControllerBase
    {
        private readonly GameServerApi.Services.GameService _gameService;
        private readonly ILogger<GameController> _logger;
        
        public GameController(GameServerApi.Services.GameService gameService, ILogger<GameController> logger)
        {
            _gameService = gameService;
            _logger = logger;
        }

        /// <summary>
        /// Extracts the user ID from the JWT claims.
        /// </summary>
        /// <returns>The user ID from the NameIdentifier claim.</returns>
        /// <exception cref="GameException">Thrown when the token is invalid or user ID cannot be parsed.</exception>
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new GameException("Invalid token", "INVALID_TOKEN", 401);
            }
            return userId;
        }


        /// <summary>
        /// Initializes a new game progression for the current user.
        /// </summary>
        /// <returns>The initialized progression object.</returns>
        /// <remarks>Note: Initialization is also done in UserController when creating a user.</remarks>
        [HttpGet("Initialize")]
        [Authorize]
        public async Task<Progression> InitializeProgression()
        {  // initialization is done in UserController when creating user so what is the point of this ? 
            var userId = GetUserId();

            var progression = await _gameService.InitializeProgressionAsync(userId);
            return progression;
        }

        /// <summary>
        /// Retrieves the current game progression for the authenticated user.
        /// </summary>
        /// <returns>The progression object containing the user's game state.</returns>
        [HttpGet("Progression")]
        [Authorize]
        public async Task<Progression> GetProgression()
        {
            var userId = GetUserId();

            var progression = await _gameService.GetProgressionAsync(userId);
            return progression;
        }

        /// <summary>
        /// Resets the game progression for the current user.
        /// </summary>
        /// <returns>The reset progression object.</returns>
        [HttpPost("Reset")]
        [Authorize]
        public async Task<Progression> ResetProgression()
        {
            var userId = GetUserId();

            var progression = await _gameService.ResetProgressionAsync(userId);
            return progression;
        }



        /// <summary>
        /// Retrieves the cost required to reset the user's game progression.
        /// </summary>
        /// <returns>A response containing the reset cost information.</returns>
        [HttpGet("ResetCost")]
        [Authorize]
        public async Task<ResetCostResponse> ResetCost()
        {
            var userId = GetUserId();

            var cost = await _gameService.GetResetCostAsync(userId);
            return cost;
        }



        /// <summary>
        /// Processes a click action for the current user.
        /// </summary>
        /// <returns>A response containing the results of the click action.</returns>
        /// <remarks>This endpoint is rate limited per user to prevent abuse.</remarks>
        [HttpGet("Click")]
        [Authorize]
        [EnableRateLimiting("perUser")]
        public async Task<ClickResponse> Click()
        {
            var userId = GetUserId();

            var response = await _gameService.ClickAsync(userId);
            return response;
        }

        /// <summary>
        /// Retrieves the best score among all players.
        /// </summary>
        /// <returns>A response containing the best score information.</returns>
        [HttpGet("BestScore")]
        [Authorize]
        public async Task<BestScoreResponse> GetBestScore()
        {
            var best = await _gameService.GetBestScoreAsync();
            return best;
        }

    }
}


