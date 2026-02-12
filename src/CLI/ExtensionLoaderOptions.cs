namespace Core.Cli;

/// <summary>
/// Configuration for extension loader directory paths.
/// Encapsulates the #if DEBUG path logic that was previously in Program.Main().
/// </summary>
public class ExtensionLoaderOptions
{
    /// <summary>
    /// Directory containing built-in extensions shipped with the CLI.
    /// Debug builds point to the parallel build output; release builds use the Extensions/ subfolder.
    /// </summary>
    public required string BuiltinExtensionsDir { get; init; }

    /// <summary>
    /// User-level extensions directory (~/.cirrus/extensions/).
    /// Supports multiple versions of extensions installed side by side.
    /// </summary>
    public required string UserExtensionsDir { get; init; }

    /// <summary>
    /// Creates options with the standard directory layout.
    /// </summary>
    public static ExtensionLoaderOptions CreateDefault()
    {
        var userExtensionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cirrus", "extensions");
#if DEBUG
        var builtinExtensionsDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, ".."));
#else
        var builtinExtensionsDir = Path.Combine(AppContext.BaseDirectory, "Extensions");
#endif
        return new ExtensionLoaderOptions
        {
            BuiltinExtensionsDir = builtinExtensionsDir,
            UserExtensionsDir = userExtensionsDir
        };
    }
}
