namespace GameServerApi.Exceptions;

/// <summary>
/// Custom exception class for game-specific errors.
/// </summary>
public class GameException : Exception
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Code { get; set; }
    
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Initializes a new instance of the GameException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The error code.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    public GameException(string message, string code, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

