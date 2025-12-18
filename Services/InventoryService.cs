using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using GameServerApi.Models;
using GameServerApi.Exceptions;

namespace GameServerApi.Services
{
    public class InventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedInventoryAsync()
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
                    throw new GameException("seed failed: items deserialization returned null", "SEED_FAILED", 500);
                }

                var validItems = items.Where(i => !string.IsNullOrWhiteSpace(i?.Name)).ToList();
                if (validItems.Count == 0)
                {
                    throw new GameException("seed failed: no valid items found", "SEED_FAILED", 500);
                }

                _context.Items.AddRange(validItems);
                await _context.SaveChangesAsync();
            }
            catch (GameException)
            {
                throw;
            }
            catch
            {
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
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new GameException("User not found", "USER_NOT_FOUND", 404);
            }

            var progression = await _context.Progressions.FirstOrDefaultAsync(p => p.UserId == userId);
            if (progression == null)
            {
                throw new GameException("User not found", "USER_NOT_FOUND", 404);
            }

            var item = await _context.Items.FindAsync(itemId);
            if (item == null)
            {
                throw new GameException("Item not found", "ITEM_NOT_FOUND", 404);
            }

            if (progression.Count < item.Price)
            {
                throw new GameException("Not enough money to buy the item", "NOT_ENOUGH_MONEY", 400);
            }

            var inventoryEntry = await _context.InventoryEntries
                .FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);

            if (inventoryEntry != null)
            {
                if (inventoryEntry.Quantity >= item.MaxQuantity)
                {
                    throw new GameException("Inventory is full", "INVENTORY_FULL", 400);
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
            return inventoryEntry;
        }

        public async Task<InventoryEntry[]> GetUserInventoryAsync(int userId)
        {
            var inventory = await _context.InventoryEntries
                .Where(i => i.UserId == userId)
                .ToArrayAsync();

            return inventory;
        }
    }
}
