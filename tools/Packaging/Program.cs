using Azclips.Packaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Azclips.Packaging.Tool <publish-dir> <extension-source-dir> [...] | --verify-archive <archive>");
    return 1;
}

try
{
    if (args[0] == "--verify-archive")
    {
        if (args.Length != 2)
        {
            throw new InvalidOperationException("--verify-archive requires exactly one CLI archive.");
        }
        PackageLayout.VerifyArchive(args[1]);
    }
    else
    {
        HostSharedDependencies.Normalize(args[0], args[1..]);
    }
    return 0;
}
catch (Exception ex) when (ex is IOException or BadImageFormatException or InvalidOperationException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}
