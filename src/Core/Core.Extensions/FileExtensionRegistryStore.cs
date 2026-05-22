// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Core.Extensions
{
    /// <summary>
    /// File-backed registry store. Default path is <c>~/.cirrus/extensions/registry.json</c>.
    /// Writes are atomic: serialize to a sibling <c>.tmp</c> file then rename over the target.
    /// </summary>
    public sealed class FileExtensionRegistryStore : IExtensionRegistryStore
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        private readonly string _path;

        /// <summary>
        /// Creates a store at <paramref name="path"/>, or at the default user-scoped location if null.
        /// </summary>
        public FileExtensionRegistryStore(string? path = null)
        {
            _path = path ?? DefaultPath();
        }

        /// <summary>
        /// Absolute path to the registry file this store reads and writes.
        /// </summary>
        public string Path => _path;

        /// <inheritdoc />
        public ExtensionRegistry Load()
        {
            if (!File.Exists(_path))
            {
                return new ExtensionRegistry();
            }

            using var stream = File.OpenRead(_path);
            var registry = JsonSerializer.Deserialize<ExtensionRegistry>(stream, s_jsonOptions)
                ?? throw new InvalidDataException($"Registry file '{_path}' deserialized to null.");

            if (registry.SchemaVersion > ExtensionRegistry.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Registry file '{_path}' has schemaVersion {registry.SchemaVersion} which is newer than the supported version {ExtensionRegistry.CurrentSchemaVersion}. Upgrade the host.");
            }

            // Rebuild the dictionary with a case-insensitive comparer; the deserializer ignores
            // the constructor-supplied comparer on the property's default value.
            registry.Extensions = new Dictionary<string, ExtensionRegistryEntry>(
                registry.Extensions,
                StringComparer.OrdinalIgnoreCase);

            return registry;
        }

        /// <inheritdoc />
        public void Save(ExtensionRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _path + ".tmp";

            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, registry, s_jsonOptions);
            }

            // Atomic rename on the same volume; overwrites any existing file.
            File.Move(tempPath, _path, overwrite: true);
        }

        /// <summary>
        /// Computes the default registry path: <c>~/.cirrus/extensions/registry.json</c>.
        /// </summary>
        public static string DefaultPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return System.IO.Path.Combine(home, ".cirrus", "extensions", "registry.json");
        }
    }
}
