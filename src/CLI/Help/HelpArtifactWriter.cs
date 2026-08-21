// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Cli.Help
{
    public static class HelpArtifactWriter
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static void WriteArtifacts(
            string outputDirectory,
            HelpManifest manifest,
            HelpIndex index,
            string manifestFileName = "HelpManifest.json",
            string indexFileName = "HelpIndex.json")
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
            }

            Directory.CreateDirectory(outputDirectory);

            var manifestPath = Path.Combine(outputDirectory, manifestFileName);
            var indexPath = Path.Combine(outputDirectory, indexFileName);

            var manifestJson = JsonSerializer.Serialize(manifest, s_jsonOptions);
            var indexJson = JsonSerializer.Serialize(index, s_jsonOptions);

            File.WriteAllText(manifestPath, manifestJson + Environment.NewLine);
            File.WriteAllText(indexPath, indexJson + Environment.NewLine);
        }
    }
}
