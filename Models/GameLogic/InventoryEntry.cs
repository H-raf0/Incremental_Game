namespace GameServerApi.Models;

public class InventoryEntry
{
	public int Id { get; set; }
	public int UserId { get; set; }
	public int ItemId { get; set; }
	public int Quantity { get; set; }

    public InventoryEntry(int userId, int itemId, int quantity)
    {
        UserId = userId;
        ItemId = itemId;
        Quantity = quantity;
    }
}