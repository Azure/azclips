namespace Core.Engine;

/// <summary>
/// Parses command-line arguments into commands and options.
/// Supports --key=value, --key value, and -k value formats.
/// </summary>
public class CommandParser
{
    /// <summary>
    /// Parses the command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>Parsed result containing commands and options</returns>
    public ParseResult Parse(string[] args)
    {
        var result = new ParseResult();
        var commands = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Handle --key=value format
            if (arg.StartsWith("--") && arg.Contains('='))
            {
                var parts = arg.Split('=', 2);
                var key = parts[0].TrimStart('-');
                var value = parts[1];
                options[key] = value;
            }
            // Handle --key value format
            else if (arg.StartsWith("--"))
            {
                var key = arg.TrimStart('-');
                // Check if next argument exists and is not another option
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    options[key] = args[i + 1];
                    i++; // Skip next argument as it's the value
                }
                else
                {
                    options[key] = "true"; // Flag without value
                }
            }
            // Handle -k value format (short options)
            else if (arg.StartsWith("-") && arg.Length == 2)
            {
                var key = arg.TrimStart('-');
                // Check if next argument exists and is not another option
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    options[key] = args[i + 1];
                    i++; // Skip next argument as it's the value
                }
                else
                {
                    options[key] = "true"; // Flag without value
                }
            }
            // Command or subcommand
            else
            {
                commands.Add(arg);
            }
        }

        result.Commands = commands;
        result.Options = options;
        return result;
    }
}
