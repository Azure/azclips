// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;
using Core.Abstractions;

namespace Core.Extensions.Tests
{
    public class FileExtensionRegistryStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _registryPath;

        public FileExtensionRegistryStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "azclips-registry-tests-" + Guid.NewGuid().ToString("N"));
            _registryPath = Path.Combine(_tempDir, "registry.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public void Load_ShouldReturnEmptyRegistry_WhenFileDoesNotExist()
        {
            var store = new FileExtensionRegistryStore(_registryPath);

            var registry = store.Load();

            Assert.Equal(ExtensionRegistry.CurrentSchemaVersion, registry.SchemaVersion);
            Assert.Empty(registry.Extensions);
        }

        [Fact]
        public void Save_ShouldCreateParentDirectory_WhenMissing()
        {
            var store = new FileExtensionRegistryStore(_registryPath);

            store.Save(new ExtensionRegistry());

            Assert.True(File.Exists(_registryPath));
        }

        [Fact]
        public void SaveThenLoad_ShouldRoundtripEntries()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            var installedAt = new DateTimeOffset(2026, 4, 12, 10, 23, 0, TimeSpan.Zero);

            var original = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
                    {
                        Verb = "vm",
                        Version = "1.1.0",
                        Pinned = false,
                        Source = "https://api.nuget.org/v3/index.json",
                        InstalledAt = installedAt,
                    },
                    ["Contoso.Foo"] = new ExtensionRegistryEntry
                    {
                        Verb = "foo",
                        Version = "0.1.0-preview",
                        Pinned = true,
                        Source = null,
                        InstalledAt = installedAt,
                    },
                },
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(2, loaded.Extensions.Count);

            var compute = loaded.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"];
            Assert.Null(compute.Verb);
            Assert.Equal("1.1.0", compute.Version);
            Assert.False(compute.Pinned);
            Assert.Equal("https://api.nuget.org/v3/index.json", compute.Source);
            Assert.Equal(installedAt, compute.InstalledAt);
            Assert.Empty(compute.Contributions);
            Assert.Empty(compute.Namespaces);

            var foo = loaded.Extensions["Contoso.Foo"];
            Assert.Null(foo.Verb);
            Assert.True(foo.Pinned);
            Assert.Null(foo.Source);
            Assert.Empty(foo.Contributions);
            Assert.Empty(foo.Namespaces);
        }

        [Fact]
        public void Load_ShouldLookupPackageIds_CaseInsensitively()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            store.Save(new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
                    {
                        Verb = "vm",
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                    },
                },
            });

            var loaded = store.Load();

            Assert.True(loaded.Extensions.ContainsKey("Microsoft.Azure.Azclips.Extensions.compute"));
            Assert.True(loaded.Extensions.ContainsKey("Microsoft.Azure.Azclips.Extensions.COMPUTE"));
        }

        [Fact]
        public void Save_ShouldNotLeaveTempFileBehind_OnSuccess()
        {
            var store = new FileExtensionRegistryStore(_registryPath);

            store.Save(new ExtensionRegistry());

            Assert.False(File.Exists(_registryPath + ".tmp"));
        }

        [Fact]
        public void Save_ShouldOverwriteExistingFile()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            store.Save(new ExtensionRegistry
            {
                Extensions =
                {
                    ["A"] = new ExtensionRegistryEntry { Verb = "a", Version = "1.0.0", InstalledAt = DateTimeOffset.UtcNow },
                },
            });

            store.Save(new ExtensionRegistry
            {
                Extensions =
                {
                    ["B"] = new ExtensionRegistryEntry { Verb = "b", Version = "2.0.0", InstalledAt = DateTimeOffset.UtcNow },
                },
            });

            var loaded = store.Load();
            Assert.Single(loaded.Extensions);
            Assert.True(loaded.Extensions.ContainsKey("B"));
        }

        [Fact]
        public void Load_ShouldThrow_OnNewerSchemaVersion()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(_registryPath, "{\"schemaVersion\":999,\"extensions\":{}}");

            var ex = Assert.Throws<InvalidDataException>(() => store.Load());
            Assert.Contains("999", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Load_ShouldThrow_OnMalformedJson()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(_registryPath, "{ not json");

            Assert.ThrowsAny<System.Text.Json.JsonException>(() => store.Load());
        }

        // -------- Schema v2 tolerance and round-trip --------

        [Fact]
        public void Load_ShouldSynthesizeContribution_FromLegacyV1Entry()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            Directory.CreateDirectory(_tempDir);
            // Hand-write a v1-style file (no contributions, no namespaces, schemaVersion=1).
            File.WriteAllText(_registryPath,
                """
                {
                  "schemaVersion": 1,
                  "extensions": {
                    "Microsoft.Azure.Azclips.Extensions.Compute": {
                      "verb": "vm",
                      "version": "1.0.0",
                      "pinned": false,
                      "installedAt": "2026-04-12T10:23:00+00:00"
                    }
                  }
                }
                """);

            var loaded = store.Load();

            var entry = loaded.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"];
            Assert.Single(entry.Contributions);
            Assert.Equal("vm", entry.Contributions[0].Verb);
            Assert.Equal(string.Empty, entry.Contributions[0].Path);
            Assert.Single(entry.Namespaces);
            Assert.Equal("vm", entry.Namespaces[0]);
            // Legacy verb field is preserved in memory for back-compat with v1 callers.
            Assert.Equal("vm", entry.Verb);
        }

        [Fact]
        public void SaveThenLoad_ShouldRoundTripMultipleContributions()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            var original = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
                    {
                        Version = "1.1.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions =
                        {
                            new Contribution { Verb = "vm",   Path = "create" },
                            new Contribution { Verb = "vm",   Path = "delete" },
                            new Contribution { Verb = "disk", Path = "attach" },
                        },
                    },
                },
            };

            store.Save(original);
            var loaded = store.Load();

            var entry = loaded.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"];
            Assert.Null(entry.Verb);
            Assert.Empty(entry.Contributions);
            Assert.Empty(entry.Namespaces);
        }

        [Fact]
        public void Save_ShouldDeriveNamespaces_FromContributions()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Contoso.MultiNs"] = new ExtensionRegistryEntry
                    {
                        Version = "0.1.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                        Contributions =
                        {
                            new Contribution { Verb = "alpha", Path = "do" },
                            new Contribution { Verb = "ALPHA", Path = "undo" }, // case duplicate
                            new Contribution { Verb = "beta",  Path = "" },
                        },
                        // Namespaces deliberately left bogus; Save should overwrite.
                        Namespaces = { "stale" },
                    },
                },
            };

            store.Save(registry);
            var loaded = store.Load();

            var entry = loaded.Extensions["Contoso.MultiNs"];
            Assert.Empty(entry.Namespaces);
        }

        [Fact]
        public void Save_ShouldNotMutateInputRegistry()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Contoso.Mutable"] = new ExtensionRegistryEntry
                    {
                        Verb = "resources",
                        Version = "0.1.0",
                        Contributions =
                        {
                            new Contribution { Verb = "resources", Path = "group create" },
                        },
                        Namespaces = { "resources" },
                        InstalledAt = DateTimeOffset.UtcNow,
                    },
                },
            };

            store.Save(registry);

            var entry = registry.Extensions["Contoso.Mutable"];
            Assert.Equal("resources", entry.Verb);
            Assert.Single(entry.Contributions);
            Assert.Equal("resources", entry.Contributions[0].Verb);
            Assert.Equal("group create", entry.Contributions[0].Path);
            Assert.Single(entry.Namespaces);
            Assert.Equal("resources", entry.Namespaces[0]);
        }

        [Fact]
        public void Save_ShouldAlwaysWriteCurrentSchemaVersion_EvenWhenLoadedAsV1()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(_registryPath,
                """
                {
                  "schemaVersion": 1,
                  "extensions": {
                    "Microsoft.Azure.Azclips.Extensions.Compute": {
                      "verb": "vm",
                      "version": "1.0.0",
                      "installedAt": "2026-04-12T10:23:00+00:00"
                    }
                  }
                }
                """);

            var registry = store.Load();
            store.Save(registry);

            var raw = File.ReadAllText(_registryPath);
            Assert.Contains("\"schemaVersion\": 2", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"contributions\"", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"namespaces\"", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"verb\"", raw, StringComparison.Ordinal);
        }

        // -------- Optimistic concurrency + load-time validation --------

        [Fact]
        public void Save_ShouldThrowConcurrency_WhenFileChangedBetweenLoadAndSave()
        {
            Directory.CreateDirectory(_tempDir);
            // Seed an initial file so Load captures a timestamp.
            File.WriteAllText(_registryPath, """{"schemaVersion":2,"extensions":{}}""");

            var store = new FileExtensionRegistryStore(_registryPath);
            var registry = store.Load();

            // Simulate another process writing the file after our Load.
            // Bump file write time deterministically.
            var newWriteTime = File.GetLastWriteTimeUtc(_registryPath).AddSeconds(5);
            File.WriteAllText(_registryPath, """{"schemaVersion":2,"extensions":{}}""");
            File.SetLastWriteTimeUtc(_registryPath, newWriteTime);

            var ex = Assert.Throws<AzclipsErrorException>(() => store.Save(registry));
            Assert.Equal("RegistryConcurrency", ex.Code);
        }

        [Fact]
        public void Save_ShouldSucceed_WhenFileDidNotExistAtLoadTime()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            var registry = store.Load(); // file absent

            registry.Extensions["Some.Package"] = new ExtensionRegistryEntry
            {
                Verb = "demo",
                Version = "1.0.0",
            };

            store.Save(registry); // must not throw

            var reloaded = new FileExtensionRegistryStore(_registryPath).Load();
            Assert.True(reloaded.Extensions.ContainsKey("Some.Package"));
        }

        [Fact]
        public void Save_ShouldSucceed_OnRepeatedSaves_FromSameInstance()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            var registry = store.Load();

            registry.Extensions["Pkg.One"] = new ExtensionRegistryEntry { Verb = "one", Version = "1.0.0" };
            store.Save(registry);

            registry.Extensions["Pkg.Two"] = new ExtensionRegistryEntry { Verb = "two", Version = "1.0.0" };
            store.Save(registry); // must not flag itself as a concurrent edit
        }

        [Fact]
        public void Load_ShouldThrow_WhenTwoEntriesClaimSameLeafContribution()
        {
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(_registryPath, """
                {
                  "schemaVersion": 2,
                  "extensions": {
                    "Pkg.A": {
                      "version": "1.0.0",
                      "contributions": [ { "verb": "vm", "path": "create" } ]
                    },
                    "Pkg.B": {
                      "version": "1.0.0",
                      "contributions": [ { "verb": "vm", "path": "create" } ]
                    }
                  }
                }
                """);

            var store = new FileExtensionRegistryStore(_registryPath);
            var ex = Assert.Throws<AzclipsErrorException>(() => store.Load());
            Assert.Equal("RegistryInvalid", ex.Code);
        }

        // --- NormalizeEntry trims whitespace and detects intra-entry dupes. ---

        [Fact]
        public void Save_ShouldTrimWhitespaceFromVerbAndPath()
        {
            Directory.CreateDirectory(_tempDir);
            var registry = new ExtensionRegistry();
            registry.Extensions["Pkg.Trim"] = new ExtensionRegistryEntry
            {
                Verb = "  resources  ",
                Version = "1.0.0",
                Contributions =
                {
                    new Contribution { Verb = "  resources  ", Path = "  group   create  " },
                    new Contribution { Verb = "deployment", Path = "   " }, // whitespace-only path -> empty
                },
                InstalledAt = DateTimeOffset.UtcNow,
            };

            new FileExtensionRegistryStore(_registryPath).Save(registry);
            var reloaded = new FileExtensionRegistryStore(_registryPath).Load();

            var entry = reloaded.Extensions["Pkg.Trim"];
            Assert.Null(entry.Verb);
            Assert.Empty(entry.Contributions);
            Assert.Empty(entry.Namespaces);
        }

        [Fact]
        public void Save_ShouldThrowRegistryInvalid_WhenSingleEntryHasDuplicateContribution()
        {
            Directory.CreateDirectory(_tempDir);
            var registry = new ExtensionRegistry();
            registry.Extensions["Pkg.Dup"] = new ExtensionRegistryEntry
            {
                Verb = "vm",
                Version = "1.0.0",
                Contributions =
                {
                    new Contribution { Verb = "vm", Path = "create" },
                    new Contribution { Verb = "vm", Path = "create" }, // intra-entry duplicate
                },
                InstalledAt = DateTimeOffset.UtcNow,
            };

            var store = new FileExtensionRegistryStore(_registryPath);
            var ex = Assert.Throws<AzclipsErrorException>(() => store.Save(registry));

            Assert.Equal("RegistryInvalid", ex.Code);
            Assert.Contains("Pkg.Dup", ex.Message);
            Assert.Contains("duplicate contribution", ex.Message);
        }

        [Fact]
        public void NormalizeEntry_ShouldExcludeEmptyVerbsFromNamespaces()
        {
            Directory.CreateDirectory(_tempDir);
                        File.WriteAllText(_registryPath, """
                                {
                                    "schemaVersion": 2,
                                    "extensions": {
                                        "Pkg.EmptyVerb": {
                                            "version": "1.0.0",
                                            "contributions": [
                                                { "verb": "real", "path": "" },
                                                { "verb": "   ", "path": "x" }
                                            ]
                                        }
                                    }
                                }
                                """);

                        var reloaded = new FileExtensionRegistryStore(_registryPath).Load();

                        Assert.Equal(new[] { "real" }, reloaded.Extensions["Pkg.EmptyVerb"].Namespaces);
        }
    }
}

