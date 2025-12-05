namespace Core.Engine;

/// <summary>
/// Parses command line input into commands and options.
/// </summary>
internal class CommandParser
{
    public ParseResult Parse(string[] args)
    {
        // todo: this is incorrect and only a placeholder implementation
        return new ParseResult()
        {
            Commands = args.Where(arg => !arg.StartsWith("--")),
            Options = args
                .Where(arg => arg.StartsWith("--"))
                .Select(arg => arg.Substring(2).Split('='))
                .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : string.Empty)
        };
    }
}

/// <summary>
/// Parsed command.
/// todo: improve this structure
/// </summary>
internal class ParseResult
{
    /// <summary>
    /// List of commands
    /// </summary>
    public IEnumerable<string> Commands { get; set; } = [];
    /// <summary>
    /// Parameter name to value mapping
    /// </summary>
    public IDictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
}
