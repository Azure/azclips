namespace Core.Cli
{
    public class PropertyParserException : Exception
    {
        public PropertyParserException(string message)
            : base(message)
        {
        }

        public PropertyParserException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}