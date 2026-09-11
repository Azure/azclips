// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Core.Extensions
{
    /// <summary>
    /// File-backed registry store. Default path is <c>~/.azclips/extensions/registry.json</c>.
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

        // Optimistic-concurrency state: timestamp captured at the most recent Load(),
        // checked at Save() to detect another writer interleaving between the two calls. A null
        // value means "we never observed the file" — in that case Save() proceeds unconditionally
        // (first-write semantics). The state is per-instance; concurrency across process
        // boundaries is the actual race we're guarding against, not in-process threading.
        private DateTime? _lastObservedWriteUtc;

        /// <summary>
        /// Creates a store at <paramref name="path"/>. Callers resolve the path — the default DI
        /// registration in <c>CliExtensionServiceRegistration</c> derives it from
        /// <c>AssemblyLoadingOptions.InstalledExtensionsRoot</c> so the config knob controls it.
        /// </summary>
        public FileExtensionRegistryStore(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            _path = path;
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
                // Sentinel: file didn't exist. Save() can still succeed without a concurrency
                // check because there's nothing to conflict with.
                _lastObservedWriteUtc = null;
                return new ExtensionRegistry();
            }

            _lastObservedWriteUtc = File.GetLastWriteTimeUtc(_path);

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

            // v1 -> v2 tolerant upgrade (in memory only): an entry with a legacy `verb` field but no
            // `contributions` is normalized into a single (verb, "") contribution.
            foreach (var entry in registry.Extensions.Values)
            {
                NormalizeEntry(entry);
            }

            // Invariant check: no two entries may claim the same (verb, path) tuple.
            // Catches a hand-edited file before the dispatcher ever consults the LeafIndex.
            ValidateNoDuplicateContributions(registry);

            return registry;
        }

        /// <inheritdoc />
        public void Save(ExtensionRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);
            var persisted = CreatePersistedSnapshot(registry);

            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Optimistic-concurrency check: if we previously observed a write time and
            // the file's current write time disagrees, another process touched it between our
            // Load and Save. Surface as a structured error so the caller can retry or inform the
            // user — silently overwriting would be the worst outcome.
            if (_lastObservedWriteUtc is { } expected && File.Exists(_path))
            {
                var current = File.GetLastWriteTimeUtc(_path);
                if (current != expected)
                {
                    throw AzclipsErrorException.RegistryConcurrency(
                        $"Registry file '{_path}' was modified by another process between this command's read " +
                        $"and write. Retry the command. (expected write time {expected:o}, current {current:o})",
                        new Dictionary<string, object?>
                        {
                            ["path"] = _path,
                            ["expectedWriteUtc"] = expected,
                            ["currentWriteUtc"] = current,
                        });
                }
            }

            var tempPath = _path + ".tmp";

            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, persisted, s_jsonOptions);
            }

            // Atomic rename on the same volume; overwrites any existing file.
            File.Move(tempPath, _path, overwrite: true);

            // Refresh our observation so a subsequent Save in the same instance doesn't
            // mistakenly flag itself as a concurrent edit.
            _lastObservedWriteUtc = File.GetLastWriteTimeUtc(_path);
        }

        private static ExtensionRegistry CreatePersistedSnapshot(ExtensionRegistry source)
        {
            var snapshot = new ExtensionRegistry
            {
                SchemaVersion = ExtensionRegistry.CurrentSchemaVersion,
                Extensions = new Dictionary<string, ExtensionRegistryEntry>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var (packageId, entry) in source.Extensions)
            {
                var clonedEntry = new ExtensionRegistryEntry
                {
                    Verb = entry.Verb,
                    Version = entry.Version,
                    Pinned = entry.Pinned,
                    Source = entry.Source,
                    InstalledAt = entry.InstalledAt,
                    Contributions = entry.Contributions
                        .Select(c => new Contribution { Verb = c.Verb, Path = c.Path })
                        .ToList(),
                    Namespaces = [.. entry.Namespaces],
                };

                NormalizeEntry(clonedEntry);

                snapshot.Extensions[packageId] = clonedEntry;
            }

            // Validate after normalization and before stripping persisted compatibility fields.
            ValidateNoDuplicateContributions(snapshot);

            // Preferences-only persisted shape: keep compatibility fields out of registry.json.
            foreach (var entry in snapshot.Extensions.Values)
            {
                entry.Verb = null;
                entry.Contributions.Clear();
                entry.Namespaces.Clear();
            }

            return snapshot;
        }

        /// <summary>
        /// Verifies the leaf-uniqueness invariant: no two contributions in the registry may
        /// declare the same <c>(verb, path)</c> tuple, whether they belong to different entries
        /// or to the same entry. Throws <see cref="AzclipsErrorException.RegistryInvalid"/> on
        /// violation so callers can recover (e.g. by editing the file).
        /// </summary>
        /// <remarks>
        /// Pre-PR-#253 the check only flagged cross-entry duplicates; an entry that declared
        /// the same contribution twice was silently accepted. The dispatcher would then route
        /// arbitrarily, masking a registry-construction bug.
        /// </remarks>
        private static void ValidateNoDuplicateContributions(ExtensionRegistry registry)
        {
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (packageId, entry) in registry.Extensions)
            {
                var perEntrySeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in entry.Contributions)
                {
                    var key = string.IsNullOrEmpty(c.Path) ? c.Verb : $"{c.Verb} {c.Path}";
                    if (!perEntrySeen.Add(key))
                    {
                        throw AzclipsErrorException.RegistryInvalid(
                            $"Registry entry '{packageId}' contains duplicate contribution '{key}'. " +
                            $"Edit registry.json to remove the duplicate, or run 'az extension remove' " +
                            $"and reinstall.",
                            new Dictionary<string, object?>
                            {
                                ["leafKey"] = key,
                                ["packageId"] = packageId,
                            });
                    }
                    if (seen.TryGetValue(key, out var owner) &&
                        !string.Equals(owner, packageId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw AzclipsErrorException.RegistryInvalid(
                            $"Registry contains duplicate contribution '{key}' claimed by both '{owner}' and '{packageId}'. " +
                            $"Edit registry.json to remove one, or run 'az extension remove' for the offender.",
                            new Dictionary<string, object?>
                            {
                                ["leafKey"] = key,
                                ["firstOwner"] = owner,
                                ["secondOwner"] = packageId,
                            });
                    }
                    seen[key] = packageId;
                }
            }
        }

        /// <summary>
        /// Brings an entry to v2 shape: synthesizes a single <c>(Verb, "")</c> contribution
        /// from the legacy <c>Verb</c> field if no contributions are present, then re-derives
        /// the <c>Namespaces</c> field from the distinct verbs in <c>Contributions</c>.
        /// Idempotent. Whitespace in <c>Verb</c> and <c>Path</c> is trimmed; whitespace-only
        /// paths are coerced to <see cref="string.Empty"/> so the dispatcher's empty-path
        /// branch is taken (avoids spurious "verb  " keys with embedded double-space).
        /// </summary>
        private static void NormalizeEntry(ExtensionRegistryEntry entry)
        {
            // Defensive trimming on hand-edited registry files. JSON deserialization preserves
            // whatever the user typed; we want canonical forms before any equality check.
            if (entry.Verb is not null)
            {
                var trimmed = entry.Verb.Trim();
                entry.Verb = string.IsNullOrEmpty(trimmed) ? null : trimmed;
            }
            for (int i = 0; i < entry.Contributions.Count; i++)
            {
                var c = entry.Contributions[i];
                var verbTrimmed = c.Verb?.Trim() ?? string.Empty;
                var pathTrimmed = c.Path?.Trim() ?? string.Empty;
                if (verbTrimmed != c.Verb || pathTrimmed != c.Path)
                {
                    entry.Contributions[i] = new Contribution { Verb = verbTrimmed, Path = pathTrimmed };
                }
            }

            if (entry.Contributions.Count == 0 && !string.IsNullOrEmpty(entry.Verb))
            {
                entry.Contributions.Add(new Contribution { Verb = entry.Verb, Path = string.Empty });
            }

            // Re-derive namespaces from the contribution set (case-insensitive distinct, preserve
            // first-seen casing). Namespaces is a denormalized view of the contribution verbs.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namespaces = new List<string>();
            foreach (var contribution in entry.Contributions)
            {
                if (string.IsNullOrEmpty(contribution.Verb))
                {
                    continue;
                }
                if (seen.Add(contribution.Verb))
                {
                    namespaces.Add(contribution.Verb);
                }
            }

            entry.Namespaces = namespaces;
        }
    }
}
