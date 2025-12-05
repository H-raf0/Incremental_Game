using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;


using GameServerApi.Models;

namespace GameServerApi.Controllers
{
    [Authorize]
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
        [AllowAnonymous]
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

        //GET /api/Inventory/Items
        [HttpGet("Items")]
        [AllowAnonymous]
        public async Task<ActionResult<Item[]>> GetAllItems()
        {
            var items = await _context.Items.ToArrayAsync();
            if(items == null || items.Length == 0)
            {
                return NotFound(new ErrorResponse("No items found", "NO_ITEMS"));
            }
            return Ok(items);
        }

        //POST /api/Inventory/Buy/{userId}/{itemId}
        [HttpPost("Buy/{userId}/{itemId}")]
        [Authorize]
        public async Task<ActionResult<InventoryEntry>> BuyItem(int userId, int itemId)
        {
            // Verify user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return BadRequest(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            
            // Verify progression
            var progression = await _context.Progressions.FirstOrDefaultAsync(p => p.UserId == userId);
            if (progression == null)
            {
                return BadRequest(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            // Verify item exists
            var item = await _context.Items.FindAsync(itemId);
            if (item == null)
            {
                return BadRequest(new ErrorResponse("Item not found", "ITEM_NOT_FOUND"));
            }

            // Check funds
            if (progression.Count < item.Price)
            {
                return BadRequest(new ErrorResponse("Not enough money to buy the item", "NOT_ENOUGH_MONEY"));
            }

            var inventoryEntry = await _context.InventoryEntries
                .FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);

            if (inventoryEntry != null)
            {
                if (inventoryEntry.Quantity >= item.MaxQuantity)
                {
                    return BadRequest(new ErrorResponse("Inventory is full", "INVENTORY_FULL"));
                }

                inventoryEntry.Quantity += 1;
            }
            else
            {
                inventoryEntry = new InventoryEntry(userId, itemId, 1);
                _context.InventoryEntries.Add(inventoryEntry);
            }

            // Deduct price from user's progression (currency)
            progression.Count -= item.Price;

            await _context.SaveChangesAsync();
            return Ok(inventoryEntry);
        }

        //GET /api/Inventory/UserInventory/{userId}
        [HttpGet("UserInventory/{userId}")]
        [Authorize]
        public async Task<ActionResult<InventoryEntry[]>> UserInventory(int userId)
        {
            var inventory = await _context.InventoryEntries
                .Where(i => i.UserId == userId)
                .ToArrayAsync();

            return Ok(inventory);
        }
    }
}