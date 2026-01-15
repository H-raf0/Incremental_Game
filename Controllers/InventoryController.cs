using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;


using GameServerApi.Models;
using GameServerApi.Exceptions;

namespace GameServerApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly GameServerApi.Services.InventoryService _inventoryService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(GameServerApi.Services.InventoryService inventoryService, ILogger<InventoryController> logger)
        {
            _inventoryService = inventoryService;
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

        // GET /api/Inventory/Seed
        [HttpGet("Seed")]
        [AllowAnonymous]
        public async Task<bool> SeedInventory()
        {
            _logger.LogInformation("SeedInventory requested");
            await _inventoryService.SeedInventoryAsync();
            _logger.LogInformation("SeedInventory completed");
            return true;
        }

        //GET /api/Inventory/Items
        [HttpGet("Items")]
        [AllowAnonymous]
        public async Task<Item[]> GetAllItems()
        {
            _logger.LogDebug("GetAllItems called");
            var items = await _inventoryService.GetAllItemsAsync();
            return items;
        }

        //POST /api/Inventory/Buy/{itemId}
        [HttpPost("Buy/{itemId}")]
        public async Task<InventoryEntry> BuyItem(int itemId)
        {
            var userId = GetUserId();
            _logger.LogInformation("User {UserId} attempts to buy item {ItemId}", userId, itemId);
            var entry = await _inventoryService.BuyItemAsync(userId, itemId);
            _logger.LogInformation("User {UserId} bought item {ItemId} (EntryId: {EntryId})", userId, itemId, entry.Id);
            return entry;
        }

        //GET /api/Inventory/UserInventory
        [HttpGet("UserInventory")]
        [Authorize]
        public async Task<InventoryEntry[]> UserInventory()
        {
            var userId = GetUserId();
            _logger.LogDebug("UserInventory requested for user {UserId}", userId);
            var inventory = await _inventoryService.GetUserInventoryAsync(userId);
            return inventory;
        }
    }
}