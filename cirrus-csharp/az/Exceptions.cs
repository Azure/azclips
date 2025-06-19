namespace AzTool
{
    public class ParserException : System.Exception
    {
        public ParserException(string message) : base(message) { }

        public ParserException(string message, System.Exception innerException) : base(message, innerException) { }
    }

    public class PropertyParserException : ParserException
    {
        public PropertyParserException(string message) : base(message) { }

        public PropertyParserException(string message, System.Exception innerException) : base(message, innerException) { }
    }
}