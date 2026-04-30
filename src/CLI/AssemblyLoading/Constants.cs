namespace Core.Cli
{
    internal static class Constants
    {
        internal const string cirrusPrefix = "Microsoft.Azure.Cirrus.Extensions.";
        // TODO(Yabo): use config service to retrieve the proper cli path in the future. Use conditional compilation for now.
    #if DEBUG
        internal static readonly string builtinInstallationDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, ".."));
        internal static readonly string versionedInstallationDir = builtinInstallationDir;
        internal static readonly string extensionFolderPrefix = cirrusPrefix;
    #else
        internal static readonly string builtinInstallationDir = Path.Combine(AppContext.BaseDirectory, "Extensions");
        internal static readonly string versionedInstallationDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cirrus", "extensions");
        internal static readonly string extensionFolderPrefix = "";
    #endif
    }
}