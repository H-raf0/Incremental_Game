using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using GameServerApi.Models;
using System.Numerics;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GameController : ControllerBase
    {


        private readonly GameServerApi.Services.GameService _gameService;
        public GameController(GameServerApi.Services.GameService gameService)
        {
            _gameService = gameService;
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return null;
            }
            return userId;
        }


        // GET /api/Game/Initialize
        [HttpGet("Initialize")]
        [Authorize]
        public async Task<ActionResult<Progression>> InitializeProgression()
        {  // initialization is done in UserController when creating user so what is the point of this ? 
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
            }

            var (Success, Progression, Error) = await _gameService.InitializeProgressionAsync(userId.Value);
            if (!Success && Error != null) return BadRequest(Error);
            return Ok(Progression);

        }

        // GET /api/Game/Progression/
        [HttpGet("Progression")]
        [Authorize]
        public async Task<ActionResult<Progression>> GetProgression()
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
            }

            var progression = await _gameService.GetProgressionAsync(userId.Value);
            if (progression == null)
            {
                return BadRequest(new ErrorResponse("No progressions found", "NO_PROGRESSION"));
            }
            return Ok(progression);
        }

        // POST /api/Game/Reset
        [HttpPost("Reset")]
        [Authorize]
        public async Task<ActionResult<Progression>> ResetProgression()
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
            }

            var (Success, Progression, Error) = await _gameService.ResetProgressionAsync(userId.Value);
            if (!Success && Error != null) return BadRequest(Error);
            return Ok(Progression);
        }



        // GET /api/Game/ResetCost
        [HttpGet("ResetCost")]
        [Authorize]
        public async Task<ActionResult<int>> ResetCost()
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
            }

            var (Success, Cost, Error) = await _gameService.GetResetCostAsync(userId.Value);
            if (!Success && Error != null) return BadRequest(Error);
            return Ok(new ResetCostResponse(Cost));
        }



        // GET /api/Game/Click
        [HttpGet("Click")]
        [Authorize]
        public async Task<ActionResult<ClickResponse>> Click()
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
            }

            var (success, response, error) = await _gameService.ClickAsync(userId.Value);
            if (!success && error != null) return BadRequest(error);
            return Ok(response);
        }

        // GET /api/Game/BestScore
        [HttpGet("BestScore")]
        [Authorize]
        public async Task<ActionResult<BestScoreResponse>> GetBestScore()
        {
            var best = await _gameService.GetBestScoreAsync();
            if (best == null) return NotFound(new ErrorResponse("No progressions found", "NO_PROGRESSIONS"));
            return Ok(best);
        }



    }
}


