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
    }
}
