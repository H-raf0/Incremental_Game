namespace GameServerApi.Models.GameLogic;

public class PassiveIncome
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal IncomePerSecond { get; set; }
    public DateTime LastCalculatedAt { get; set; }
}
