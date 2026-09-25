// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IConfigurationSchema
{
    IReadOnlyList<ConfigurationKeyMetadata> GetAllKeys();

    ConfigurationKeyMetadata? GetKeyMetadata(string key);

    bool IsKnownKey(string key);

    string GetStorageKey(string key) => GetKeyMetadata(key)?.StorageKey ?? GetKeyMetadata(key)?.Key ?? key;

    string GetPublicKey(string storageKey) => GetAllKeys()
        .FirstOrDefault(metadata => string.Equals(metadata.StorageKey ?? metadata.Key, storageKey, StringComparison.OrdinalIgnoreCase))?.Key
        ?? storageKey;
}
