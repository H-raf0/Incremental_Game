using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using GameServerApi.Models;
using System.Numerics;
using GameServerApi.Exceptions;

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

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new GameException("Invalid token", "INVALID_TOKEN", 401);
            }
            return userId;
        }


        // GET /api/Game/Initialize
        [HttpGet("Initialize")]
        [Authorize]
        public async Task<Progression> InitializeProgression()
        {  // initialization is done in UserController when creating user so what is the point of this ? 
            var userId = GetUserId();

            var progression = await _gameService.InitializeProgressionAsync(userId);
            return progression;
        }

        // GET /api/Game/Progression/
        [HttpGet("Progression")]
        [Authorize]
        public async Task<Progression> GetProgression()
        {
            var userId = GetUserId();

            var progression = await _gameService.GetProgressionAsync(userId);
            return progression;
        }

        // POST /api/Game/Reset
        [HttpPost("Reset")]
        [Authorize]
        public async Task<Progression> ResetProgression()
        {
            var userId = GetUserId();

            var progression = await _gameService.ResetProgressionAsync(userId);
            return progression;
        }



        // GET /api/Game/ResetCost
        [HttpGet("ResetCost")]
        [Authorize]
        public async Task<int> ResetCost()
        {
            var userId = GetUserId();

            var cost = await _gameService.GetResetCostAsync(userId);
            return cost;
        }



        // GET /api/Game/Click
        [HttpGet("Click")]
        [Authorize]
        public async Task<ClickResponse> Click()
        {
            var userId = GetUserId();

            var response = await _gameService.ClickAsync(userId);
            return response;
        }

        // GET /api/Game/BestScore
        [HttpGet("BestScore")]
        [Authorize]
        public async Task<BestScoreResponse> GetBestScore()
        {
            var best = await _gameService.GetBestScoreAsync();
            return best;
        }

    }
}


