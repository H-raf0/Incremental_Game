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

        private readonly GameServerApi.Services.InventoryService _inventoryService;
        public InventoryController(GameServerApi.Services.InventoryService inventoryService)
        {
            _inventoryService = inventoryService;
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

        // GET /api/Inventory/Seed
        [HttpGet("Seed")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> SeedInventory()
        {
            var (Success, Error) = await _inventoryService.SeedInventoryAsync();
            if (!Success && Error != null) return BadRequest(Error);
            return Ok(true);
        }

        //GET /api/Inventory/Items
        [HttpGet("Items")]
        [AllowAnonymous]
        public async Task<ActionResult<Item[]>> GetAllItems()
        {
            var items = await _inventoryService.GetAllItemsAsync();
            if (items == null || items.Length == 0)
            {
                return NotFound(new ErrorResponse("No items found", "NO_ITEMS"));
            }
            return Ok(items);
        }

        //POST /api/Inventory/Buy/{itemId}
        [HttpPost("Buy/{itemId}")]
        public async Task<ActionResult<InventoryEntry>> BuyItem(int itemId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));

            var (Success, Entry, Error) = await _inventoryService.BuyItemAsync(userId.Value, itemId);
            if (!Success && Error != null) return BadRequest(Error);
            return Ok(Entry);
        }

        //GET /api/Inventory/UserInventory
        [HttpGet("UserInventory")]
        [Authorize]
        public async Task<ActionResult<InventoryEntry[]>> UserInventory()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
            var inventory = await _inventoryService.GetUserInventoryAsync(userId.Value);
            return Ok(inventory);
        }
    }
}