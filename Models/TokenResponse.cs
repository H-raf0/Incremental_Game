namespace GameServerApi.Models;

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } // en secondes
}

public class TokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
