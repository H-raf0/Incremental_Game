using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq;


using GameServerApi.Models;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        public InventoryController(ApplicationDbContext ctx)
        {
            _context = ctx;
        }

        // GET /api/Inventory/Seed
        [HttpGet("Seed")]
        public async Task<ActionResult<bool>> SeedInventory()
        {
            try
            {
                _context.Items.RemoveRange(_context.Items);
                _context.InventoryEntries.RemoveRange(_context.InventoryEntries);

                var client = new HttpClient();
                string json = await client.GetStringAsync("https://csharp.nouvet.fr/front4/items.json");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = JsonSerializer.Deserialize<List<Item>>(json, options);

                if (items == null)
                {
                    return BadRequest(new ErrorResponse(
                    "seed failed: items deserialization returned null",
                    "SEED_FAILED"
                    ));
                }

                // Filter out items that don't have a valid Name (prevents NOT NULL constraint failure)
                var validItems = items.Where(i => !string.IsNullOrWhiteSpace(i?.Name)).ToList();
                if (validItems.Count == 0)
                {
                    return BadRequest(new ErrorResponse(
                        "seed failed: no valid items found",
                        "SEED_FAILED"
                    ));
                }

                _context.Items.AddRange(validItems);
                await _context.SaveChangesAsync();

                return Ok(true);
            }
            catch
            {
                return BadRequest(new ErrorResponse(
                    "seed failed: an exception occurred",
                    "SEED_FAILED"
                    ));
            }
        }





        //GET /api/Inventory/UserInventory/{userId}

        [HttpGet("UserInventory/{userId}")]
        public async Task<ActionResult<InventoryEntry[]>> UserInventory(int userId)
        {
            var inventory = await _context.InventoryEntries
                .Where(i => i.UserId == userId)
                .ToArrayAsync();

            return Ok(inventory);
        }
    }
}