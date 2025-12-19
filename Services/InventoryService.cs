using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using GameServerApi.Models;
using GameServerApi.Exceptions;
using Microsoft.Extensions.Logging;

namespace GameServerApi.Services
{
    public class InventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(ApplicationDbContext context, ILogger<InventoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedInventoryAsync()
        {
            _logger.LogInformation("Inventory seeding started");
            
            try
            {
                _context.Items.RemoveRange(_context.Items);
                _context.InventoryEntries.RemoveRange(_context.InventoryEntries);

                var client = new HttpClient();
                string json = await client.GetStringAsync("https://csharp.nouvet.fr/front4/items.json");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var items = JsonSerializer.Deserialize<List<Item>>(json, options) ?? throw new GameException("seed failed: items deserialization returned null", "SEED_FAILED", 500);
                var validItems = items.Where(i => !string.IsNullOrWhiteSpace(i?.Name)).ToList();
                if (validItems.Count == 0)
                {
                    throw new GameException("seed failed: no valid items found", "SEED_FAILED", 500);
                }

                _context.Items.AddRange(validItems);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Inventory seeding completed successfully: {ItemCount} items added", validItems.Count);
            }
            catch (GameException)
            {
                _logger.LogError("Inventory seeding failed: GameException occurred");
                throw;
            }
            catch
            {
                _logger.LogError("Inventory seeding failed: An exception occurred");
                throw new GameException("seed failed: an exception occurred", "SEED_FAILED", 500);
            }
        }

        public async Task<Item[]> GetAllItemsAsync()
        {
            var items = await _context.Items.ToArrayAsync();
            if (items == null || items.Length == 0)
            {
                throw new GameException("No items found", "NO_ITEMS", 404);
            }
            return items;
        }

        public async Task<InventoryEntry> BuyItemAsync(int userId, int itemId)
        {
            _logger.LogInformation("Item purchase attempt: UserId {UserId}, ItemId {ItemId}", userId, itemId);
            
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var user = await _context.Users.FindAsync(userId) ?? throw new GameException("User not found", "USER_NOT_FOUND", 404);
                
                var progression = await _context.Progressions
                    .FirstOrDefaultAsync(p => p.UserId == userId) ?? throw new GameException("Progression not found", "PROGRESSION_NOT_FOUND", 404);
                
                var item = await _context.Items.FindAsync(itemId) ?? throw new GameException("Item not found", "ITEM_NOT_FOUND", 404);
                
                if (progression.Count < item.Price)
                {
                    _logger.LogWarning("Item purchase failed: Not enough money - UserId {UserId}, ItemId {ItemId}, Available: {Available}, Required: {Required}", userId, itemId, progression.Count, item.Price);
                    throw new GameException("Not enough money", "NOT_ENOUGH_MONEY", 400);
                }

                var inventoryEntry = await _context.InventoryEntries
                    .FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);

                if (inventoryEntry != null)
                {
                    if (inventoryEntry.Quantity >= item.MaxQuantity)
                    {
                        _logger.LogWarning("Item purchase failed: Inventory full - UserId {UserId}, ItemId {ItemId}", userId, itemId);
                        throw new GameException("Inventory full", "INVENTORY_FULL", 400);
                    }

                    inventoryEntry.Quantity += 1;
                }
                else
                {
                    inventoryEntry = new InventoryEntry(userId, itemId, 1);
                    _context.InventoryEntries.Add(inventoryEntry);
                }

                progression.Count -= item.Price;
                progression.totalClickValue += item.ClickValue;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                
                _logger.LogInformation("Item purchased successfully: UserId {UserId}, ItemId {ItemId}, ItemName: {ItemName}, Quantity: {Quantity}", userId, itemId, item.Name, inventoryEntry.Quantity);

                return inventoryEntry;
            }
            catch
            {
                _logger.LogError("Item purchase failed: UserId {UserId}, ItemId {ItemId}", userId, itemId);
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<InventoryEntry[]> GetUserInventoryAsync(int userId)
        {
            var inventory = await _context.InventoryEntries
                .Where(i => i.UserId == userId)
                .ToArrayAsync() ?? throw new GameException("Inventory not found", "INVENTORY_NOT_FOUND", 404);            

            return inventory;
        }
    }
}
