using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;


using GameServerApi.Models;
using GameServerApi.Exceptions;
using GameServerApi;

namespace GameServerApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly GameServerApi.Services.InventoryService _inventoryService;
        private readonly ILogger<InventoryController> _logger;
        private readonly IHubContext<ChatHub> _hubContext;

        public InventoryController(GameServerApi.Services.InventoryService inventoryService, ILogger<InventoryController> logger, IHubContext<ChatHub> hubContext)
        {
            _inventoryService = inventoryService;
            _logger = logger;
            _hubContext = hubContext;
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
        /// Seeds the inventory database with items from the items.json file.
        /// </summary>
        /// <returns>A boolean indicating success.</returns>
        [HttpGet("Seed")]
        [AllowAnonymous]
        public async Task<bool> SeedInventory()
        {
            _logger.LogInformation("SeedInventory requested");
            await _inventoryService.SeedInventoryAsync();
            _logger.LogInformation("SeedInventory completed");
            return true;
        }

        /// <summary>
        /// Retrieves all available items in the inventory.
        /// </summary>
        /// <returns>An array of all items.</returns>
        [HttpGet("Items")]
        [AllowAnonymous]
        public async Task<Item[]> GetAllItems()
        {
            _logger.LogDebug("GetAllItems called");
            var items = await _inventoryService.GetAllItemsAsync();
            return items;
        }

        /// <summary>
        /// Purchases an item for the current user.
        /// </summary>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <returns>The created or updated inventory entry.</returns>
        /// <remarks>Expensive items (over 10000 price) trigger a system announcement in chat.</remarks>
        [HttpPost("Buy/{itemId}")]
        [EnableRateLimiting("perUser")]
        public async Task<InventoryEntry> BuyItem(int itemId)
        {
            var userId = GetUserId();
            _logger.LogInformation("User {UserId} attempts to buy item {ItemId}", userId, itemId);
            var entry = await _inventoryService.BuyItemAsync(userId, itemId);
            _logger.LogInformation("User {UserId} bought item {ItemId} (EntryId: {EntryId})", userId, itemId, entry.Id);

            // Fetch item to check price
            var item = await _inventoryService.GetItemByIdAsync(itemId);
            if (item.Price > 10000)
            {
                var username = await _inventoryService.GetUsernameAsync(userId);
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "SYSTEM", $"{username} vient d'acquérir {item.Name} !");
            }

            return entry;
        }

        /// <summary>
        /// Retrieves the inventory for the current authenticated user.
        /// </summary>
        /// <returns>An array of inventory entries for the user.</returns>
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