using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

/// <summary>
/// Result of top-level CLI arg routing — extension name and remaining raw args.
/// The command name is embedded in <see cref="CommandArgs"/> as positional tokens
/// and will be extracted by <see cref="ArgumentParser"/> during argument parsing.
/// </summary>
public record RoutedCommand(string ExtensionName, string GroupName, string[] CommandArgs);

/// <summary>
/// Routes raw CLI args to the correct extension by inspecting args[0].
/// This is intentionally thin — it only identifies the target extension and
/// passes the remaining raw args through for later parsing by <see cref="CommandResolver"/>.
/// </summary>
public class CommandRouter
{
    public RoutedCommand Route(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));
        var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
        if (args.Length == 1 && helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase)) throw new DispatchHelpException(this);
        string groupName = $"{char.ToUpperInvariant(args[0][0])}{args[0][1..]}";
        string extensionName = CommandGroupIndex.getExtensionNameByCommandGroupName(groupName);

        return new RoutedCommand(extensionName, groupName, args[1..]);
    }
}
