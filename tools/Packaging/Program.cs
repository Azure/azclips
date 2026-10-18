using Azclips.Packaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Azclips.Packaging.Tool <publish-dir> <extension-source-dir> [...]");
    return 1;
}

try
{
    HostSharedDependencies.Normalize(args[0], args[1..]);
    return 0;
}
catch (Exception ex) when (ex is IOException or BadImageFormatException or InvalidOperationException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}
