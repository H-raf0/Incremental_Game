namespace GameServerApi.Exceptions
{
    public class GameException : Exception
    {
        public string Code { get; set; }
        public int StatusCode { get; set; }

        public GameException(string message, string code, int statusCode) : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }
    }
}

