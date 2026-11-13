// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Extensions;

namespace Azclips.ExtensionSidecar;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                "Usage: Azclips.ExtensionSidecar <extension-dir> <core-dir> <runtime-dir> <package-id> <version> <output-file>");
            return 1;
        }

        try
        {
            var (declaredVerbs, declaredLeaves) = LeafReader.ReadContract(args[0], args[1], args[2]);
            var verbs = new SortedSet<string>(declaredVerbs, StringComparer.OrdinalIgnoreCase);
            var leaves = new SortedSet<string>(declaredLeaves, StringComparer.OrdinalIgnoreCase);
            var orphanLeaves = new List<string>();
            foreach (var leaf in leaves)
            {
                var space = leaf.IndexOf(' ');
                var firstToken = space < 0 ? leaf : leaf[..space];
                if (!verbs.Contains(firstToken))
                {
                    orphanLeaves.Add(
                        $"  '{leaf}' (first token '{firstToken}' is not declared via [ExtensionVerb]; declared verbs: [{string.Join(", ", verbs)}])");
                }
            }

            if (orphanLeaves.Count > 0)
            {
                Console.Error.WriteLine(
                    $"'{args[3]}' declares leaves whose first token is not a declared verb:\n" +
                    string.Join("\n", orphanLeaves) +
                    "\nEvery leaf's first token must match one of the extension's [ExtensionVerb] declarations.");
                return 1;
            }

            var payload = new
            {
                schemaVersion = 1,
                generatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                packageId = args[3],
                version = string.IsNullOrEmpty(args[4]) ? "0.0.0" : args[4],
                verbs,
                leaves,
            };
            var outputDirectory = Path.GetDirectoryName(args[5]);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(args[5], JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(
                $"[ExtensionSidecar] Wrote {leaves.Count} leaf/leaves, {verbs.Count} verb(s) for '{args[3]}' to {args[5]}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExtensionSidecar] {ex}");
            return 1;
        }
    }
}
