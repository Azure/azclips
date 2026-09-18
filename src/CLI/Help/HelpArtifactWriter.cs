// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
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
            var elapsed = Stopwatch.StartNew();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Serialize publishers across processes as well as threads. Keep
                    // the lock file: deleting it would let waiters lock different files.
                    return new FileStream(Path.Combine(directory, CatalogLockFileName),
                        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(5))
                {
                    ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(25));
                }
            }
        }

        private static void WriteAtomic<T>(string path, T value, CancellationToken ct)
        {
            var pending = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.pending";
            try
            {
                File.WriteAllText(pending, JsonSerializer.Serialize(value, s_jsonOptions) + Environment.NewLine);
                ct.ThrowIfCancellationRequested();
                File.Move(pending, path, overwrite: true);
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
