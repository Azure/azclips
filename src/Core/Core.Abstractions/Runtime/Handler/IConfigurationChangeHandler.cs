// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler;

public interface IConfigurationChangeHandler
{
    /// <summary>
    /// Runs after a configuration key registered for this handler is set or deleted.
    /// A deleted key is passed with a null value.
    /// </summary>
    Task OnChangedAsync(string key, object? value, CancellationToken cancellationToken);
}
