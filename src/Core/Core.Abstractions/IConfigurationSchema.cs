// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IConfigurationSchema
{
    IReadOnlyList<ConfigurationKeyMetadata> GetAllKeys();

    ConfigurationKeyMetadata? GetKeyMetadata(string key);

    bool IsKnownKey(string key);
}
