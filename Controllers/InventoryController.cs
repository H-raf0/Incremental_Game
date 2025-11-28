using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        // GET /api/Inventory/Seed
        [HttpGet("Seed")]
        public async Task<ActionResult<bool>> SeedInventory()
        {
            /*
            try
            {
                _context.Items.RemoveRange(_context.Items);
                _context.Inventories.RemoveRange(_context.Inventories);
                await _context.SaveChangesAsync();

                var client = new HttpClient();
                string json = await client.GetStringAsync("https://csharp.nouvet.fr/front4/items.json");

                var items = JsonSerializer.Deserialize<List<Item>>(json);

                if (items == null)
                    return false;

                _context.Items.AddRange(items);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }*/
            return Ok(true);
        }


    }
}
