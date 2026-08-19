// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azclips.Cli.Export;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

var options = new ExporterOptions(
    ArtifactsRoot: GetArg(args, "--artifacts-root") ?? Path.Combine(repoRoot, "artifacts", "Debug"),
    OutputFolder: GetArg(args, "--output-folder") ?? Path.Combine(repoRoot, "artifacts", "cli-export"),
    ExtensionFilter: GetArg(args, "--extension"));

return Exporter.Run(options);

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Flag '{name}' requires a value.");
                Environment.Exit(2);
            }
            return args[i + 1];
        }
    }

    return null;
}
