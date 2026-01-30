namespace GameServerApi.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RefreshToken(int userId, string token, DateTime expiryDate)
    {
        UserId = userId;
        Token = token;
        ExpiryDate = expiryDate;
    }

    protected RefreshToken() { }

    public bool IsValid()
    {
        return !IsRevoked && ExpiryDate > DateTime.UtcNow;
    }
}
