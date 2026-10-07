// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface ISourceAwareConfigurationService
{
    Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default);
}
