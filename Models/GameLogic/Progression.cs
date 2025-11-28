
public record ResetCostResponse(int Cost);
public record ClickResponse(int Count, int Multiplier);
public record BestScoreResponse(int UserId, int BestScore);
public class GlobalScore
{
    public static int UserId { get; set; } = 0;
    public static int BestScore { get; set; } = 0;
}
public class Progression
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Count { get; set; }
    public int Multiplier { get; set; }

    // ?
    public Progression(int userId)
    {
        UserId = userId;
        Count = 0;
        Multiplier = 1;
    }

    public int CalculateResetCost()
    {
        // Exponential cost: 100 * (1.5^(multiplier-1))
        double baseCost = 100.0;
        double growthFactor = 1.5;
        double cost = baseCost * Math.Pow(growthFactor, Multiplier - 1);
        return (int)Math.Floor(cost);
    }

}
