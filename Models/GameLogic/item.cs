namespace GameServerApi.Models;

public class Item
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int Price { get; set; }
	public int MaxQuantity { get; set; }
	public int ClickValue { get; set; }

    public Item(int id, string name, int price, int maxQuantity, int clickValue)
    {
        Id = id;
        Name = name;
        Price = price;
        MaxQuantity = maxQuantity;
        ClickValue = clickValue;
    }
}

