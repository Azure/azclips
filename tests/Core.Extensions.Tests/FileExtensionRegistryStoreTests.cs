// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class FileExtensionRegistryStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _registryPath;

        public FileExtensionRegistryStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "cirrus-registry-tests-" + Guid.NewGuid().ToString("N"));
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
                    ["Microsoft.Azure.Cirrus.Extensions.Compute"] = new ExtensionRegistryEntry
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

            var compute = loaded.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"];
            Assert.Equal("vm", compute.Verb);
            Assert.Equal("1.1.0", compute.Version);
            Assert.False(compute.Pinned);
            Assert.Equal("https://api.nuget.org/v3/index.json", compute.Source);
            Assert.Equal(installedAt, compute.InstalledAt);

            var foo = loaded.Extensions["Contoso.Foo"];
            Assert.Equal("foo", foo.Verb);
            Assert.True(foo.Pinned);
            Assert.Null(foo.Source);
        }

        [Fact]
        public void Load_ShouldLookupPackageIds_CaseInsensitively()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            store.Save(new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Cirrus.Extensions.Compute"] = new ExtensionRegistryEntry
                    {
                        Verb = "vm",
                        Version = "1.0.0",
                        InstalledAt = DateTimeOffset.UtcNow,
                    },
                },
            });

            var loaded = store.Load();

            Assert.True(loaded.Extensions.ContainsKey("microsoft.azure.cirrus.extensions.compute"));
            Assert.True(loaded.Extensions.ContainsKey("MICROSOFT.AZURE.CIRRUS.EXTENSIONS.COMPUTE"));
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

        [Fact]
        public void DefaultPath_ShouldPointToUserProfile()
        {
            var path = FileExtensionRegistryStore.DefaultPath();

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Assert.StartsWith(home, path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine(".cirrus", "extensions", "registry.json"), path, StringComparison.OrdinalIgnoreCase);
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
                    "Microsoft.Azure.Cirrus.Extensions.Compute": {
                      "verb": "vm",
                      "version": "1.0.0",
                      "pinned": false,
                      "installedAt": "2026-04-12T10:23:00+00:00"
                    }
                  }
                }
                """);

            var loaded = store.Load();

            var entry = loaded.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"];
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
                    ["Microsoft.Azure.Cirrus.Extensions.Compute"] = new ExtensionRegistryEntry
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

            var entry = loaded.Extensions["Microsoft.Azure.Cirrus.Extensions.Compute"];
            Assert.Equal(3, entry.Contributions.Count);
            Assert.Contains(entry.Contributions, c => c.Verb == "vm"   && c.Path == "create");
            Assert.Contains(entry.Contributions, c => c.Verb == "vm"   && c.Path == "delete");
            Assert.Contains(entry.Contributions, c => c.Verb == "disk" && c.Path == "attach");

            // Namespaces is a derived view: distinct (case-insensitive), first-seen order.
            Assert.Equal(new[] { "vm", "disk" }, entry.Namespaces);
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
            Assert.Equal(new[] { "alpha", "beta" }, entry.Namespaces);
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
                    "Microsoft.Azure.Cirrus.Extensions.Compute": {
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
            Assert.Contains("\"contributions\"", raw, StringComparison.Ordinal);
            Assert.Contains("\"namespaces\"", raw, StringComparison.Ordinal);
        }

        // -------- Optimistic concurrency + load-time validation + InstallRoot --------

        [Fact]
        public void InstallRoot_ShouldReturnParentDirectoryOfRegistryPath()
        {
            var store = new FileExtensionRegistryStore(_registryPath);
            Assert.Equal(_tempDir, store.InstallRoot);
        }

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

            var ex = Assert.Throws<CirrusErrorException>(() => store.Save(registry));
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
            var ex = Assert.Throws<CirrusErrorException>(() => store.Load());
            Assert.Equal("RegistryInvalid", ex.Code);
        }

        [Fact]
        public void LoadReconciled_ShouldDropEntries_WithMissingInstallDirectory()
        {
            Directory.CreateDirectory(_tempDir);
            // Create payload only for Pkg.A.
            Directory.CreateDirectory(Path.Combine(_tempDir, "Pkg.A", "1.0.0"));

            File.WriteAllText(_registryPath, """
                {
                  "schemaVersion": 2,
                  "extensions": {
                    "Pkg.A": { "version": "1.0.0", "contributions": [ { "verb": "a", "path": "" } ] },
                    "Pkg.B": { "version": "1.0.0", "contributions": [ { "verb": "b", "path": "" } ] }
                  }
                }
                """);

            var store = new FileExtensionRegistryStore(_registryPath);
            var live = store.LoadReconciled();

            Assert.Single(live.Extensions);
            Assert.True(live.Extensions.ContainsKey("Pkg.A"));
        }
    }
}
