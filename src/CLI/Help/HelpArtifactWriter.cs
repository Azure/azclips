// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Cli.Help
{
    public static class HelpArtifactWriter
    {
        internal const string CatalogLockFileName = ".help-catalog.lock";

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static void WriteCatalog(
            string outputDirectory,
            HelpCatalogHeader header,
            IList<HelpManifest> manifests,
            HelpIndex index,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(outputDirectory);
            using var lease = AcquireCatalogLock(outputDirectory, ct);
            WriteAtomic(Path.Combine(outputDirectory, "HelpManifest.json"), new HelpManifestDocument(header, manifests), ct);
            WriteAtomic(Path.Combine(outputDirectory, "HelpIndex.json"), new HelpIndexDocument(header, index), ct);
        }

        private static FileStream AcquireCatalogLock(string directory, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return FileLock.TryAcquireAsync(Path.Combine(directory, CatalogLockFileName), FileLock.DefaultWait, ct)
                    .GetAwaiter().GetResult()
                ?? throw new IOException($"The help catalog in '{directory}' is locked by another process.");
        }

        private static void WriteAtomic<T>(string path, T value, CancellationToken ct)
        {
            var pending = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.pending";
            try
            {
                File.WriteAllText(pending, JsonSerializer.Serialize(value, s_jsonOptions) + Environment.NewLine);
                ct.ThrowIfCancellationRequested();
                if (File.Exists(path))
                    File.Replace(pending, path, destinationBackupFileName: null);
                else
                    File.Move(pending, path);
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
            }
        }

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
