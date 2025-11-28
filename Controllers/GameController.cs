using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using GameServerApi.Models;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        public GameController(ApplicationDbContext  ctx)
        {
            _context = ctx;
        }


        // GET /api/Game/Initialize/{userId}
        [HttpGet("Initialize/{userId}")]
        public async Task<ActionResult<Progression>> InitializeProgression(int userId)
        {  // initialization is done in UserController when creating user so what is the point of this ? 

            bool exists = await _context.Progressions.AnyAsync(p => p.UserId == userId);
            if (exists)
            {
                return BadRequest(new ErrorResponse(
                    "Progression already exists",
                    "PROGRESSION_EXISTS"
                ));
            }

            try
            {
                var progression = new Progression(userId);
                _context.Progressions.Add(progression);
                await _context.SaveChangesAsync();
                return Ok(progression);
            }
            catch
            {
                return BadRequest(new ErrorResponse(
                    "Failed to initialize",
                    "INITIALIZATION_FAILED"
                ));
            }

        }

        // GET /api/Game/Progression/{userId}
        [HttpGet("Progression/{userId}")]
        public async Task<ActionResult<Progression>> GetProgression(int userId)
        {
            var progression = await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();

            if (progression == null)
            {
                return BadRequest(new ErrorResponse("No progressions found", "NO_PROGRESSION"));
            }

            return Ok(progression);
        }

        // POST /api/Game/Reset/{userId}
        [HttpPost("Reset/{userId}")]
        public async Task<ActionResult<Progression>> ResetProgression(int userId)
        {
            // The progression is linked to user by UserId, not by primary key
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                return BadRequest(new ErrorResponse("No progression", "NO_PROGRESSION"));
            }

            // Check reset cost
            var resetCost = progression.CalculateResetCost();
            if (resetCost > progression.Count)
            {
                return BadRequest(new ErrorResponse("Not enough clicks to reset", "INSUFFICIENT_CLICKS"));
            }

            // update personal best score if current count is higher
            if (progression.Count > progression.BestScore)
            {
                progression.BestScore = progression.Count;
            }

            // Apply reset
            progression.Count = 0;
            progression.Multiplier++;


            // vider la db
            var inventoryEntries = _context.InventoryEntries.Where(i => i.UserId == userId);
            _context.InventoryEntries.RemoveRange(inventoryEntries);

            await _context.SaveChangesAsync();

            return Ok(progression);
        }



        // GET /api/Game/ResetCost/{userId}
        [HttpGet("ResetCost/{userId}")]
        public async Task<ActionResult<int>> ResetCost(int userId)
        {
            var progression = await _context.Progressions
                .Where(p => p.UserId == userId)
                .FirstOrDefaultAsync();

            if (progression == null)
            {
                return BadRequest(new ErrorResponse("No progressions found", "NO_PROGRESSION"));
            }

            int cost = progression.CalculateResetCost();
            return Ok(new ResetCostResponse(cost));
        }



        // GET /api/Game/Click/{userId}
        [HttpGet("Click/{userId}")]
        public async Task<ActionResult<ClickResponse>> Click(int userId)
        {
            var progression = await _context.Progressions
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (progression == null)
            {
                return BadRequest(new ErrorResponse("No progressions found", "NO_PROGRESSION"));
            }

            // valeur ajouter des equipements
            var inventoryEntries = await _context.InventoryEntries
                .Where(i => i.UserId == userId)
                .ToListAsync();
            int bonus = inventoryEntries.Sum(entry =>
            {
                var item = _context.Items.Find(entry.ItemId);
                return item != null ? item.ClickValue * entry.Quantity : 0;
            });

            progression.Count += progression.Multiplier + bonus;

            await _context.SaveChangesAsync();

            return Ok(new ClickResponse(progression.Count, progression.Multiplier));
        }

        // GET /api/Game/BestScore
        [HttpGet("BestScore")]
        public async Task<ActionResult<BestScoreResponse>> GetBestScore()
        {
            var bestProgression = await _context.Progressions
                .OrderByDescending(p => p.BestScore)
                .FirstOrDefaultAsync();


            // Return the global best score and its owner
            if (bestProgression == null || bestProgression.BestScore == 0)
            {
                return NotFound(new ErrorResponse("No progressions found", "NO_PROGRESSIONS"));
            }

            return Ok(new BestScoreResponse(bestProgression.UserId, bestProgression.BestScore));
        }



    }
}


