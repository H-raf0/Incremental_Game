using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using GameServerApi.Models;

namespace GameServerApi.Services
{
    public class InventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, ErrorResponse? Error)> SeedInventoryAsync()
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
                    return (false, new ErrorResponse("seed failed: items deserialization returned null", "SEED_FAILED"));
                }

                var validItems = items.Where(i => !string.IsNullOrWhiteSpace(i?.Name)).ToList();
                if (validItems.Count == 0)
                {
                    return (false, new ErrorResponse("seed failed: no valid items found", "SEED_FAILED"));
                }

                _context.Items.AddRange(validItems);
                await _context.SaveChangesAsync();

                return (true, null);
            }
            catch
            {
                return (false, new ErrorResponse("seed failed: an exception occurred", "SEED_FAILED"));
            }
        }

        public async Task<Item[]?> GetAllItemsAsync()
        {
            var items = await _context.Items.ToArrayAsync();
            return items;
        }

        public async Task<(bool Success, InventoryEntry? Entry, ErrorResponse? Error)> BuyItemAsync(int userId, int itemId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, null, new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            var progression = await _context.Progressions.FirstOrDefaultAsync(p => p.UserId == userId);
            if (progression == null)
            {
                return (false, null, new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            var item = await _context.Items.FindAsync(itemId);
            if (item == null)
            {
                return (false, null, new ErrorResponse("Item not found", "ITEM_NOT_FOUND"));
            }

            if (progression.Count < item.Price)
            {
                return (false, null, new ErrorResponse("Not enough money to buy the item", "NOT_ENOUGH_MONEY"));
            }

            var inventoryEntry = await _context.InventoryEntries
                .FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);

            if (inventoryEntry != null)
            {
                if (inventoryEntry.Quantity >= item.MaxQuantity)
                {
                    return (false, null, new ErrorResponse("Inventory is full", "INVENTORY_FULL"));
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
            return (true, inventoryEntry, null);
        }

        public async Task<InventoryEntry[]?> GetUserInventoryAsync(int userId)
        {
            var inventory = await _context.InventoryEntries
                .Where(i => i.UserId == userId)
                .ToArrayAsync();

            return inventory;
        }
    }
}
