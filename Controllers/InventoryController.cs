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
        public InventoryController(GameServerApi.Services.InventoryService inventoryService)
        {
            _inventoryService = inventoryService;
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
            await _inventoryService.SeedInventoryAsync();
            return true;
        }

        //GET /api/Inventory/Items
        [HttpGet("Items")]
        [AllowAnonymous]
        public async Task<Item[]> GetAllItems()
        {
            var items = await _inventoryService.GetAllItemsAsync();
            return items;
        }

        //POST /api/Inventory/Buy/{itemId}
        [HttpPost("Buy/{itemId}")]
        public async Task<InventoryEntry> BuyItem(int itemId)
        {
            var userId = GetUserId();
            var entry = await _inventoryService.BuyItemAsync(userId, itemId);
            return entry;
        }

        //GET /api/Inventory/UserInventory
        [HttpGet("UserInventory")]
        [Authorize]
        public async Task<InventoryEntry[]> UserInventory()
        {
            var userId = GetUserId();
            var inventory = await _inventoryService.GetUserInventoryAsync(userId);
            return inventory;
        }
    }
}