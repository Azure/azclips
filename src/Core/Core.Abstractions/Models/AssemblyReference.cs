// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Identifies an assembly selected by a client without exposing how it is located or loaded.
/// References are runtime-only and must not be persisted.
/// </summary>
public abstract class AssemblyReference
{
    private readonly Action _validateUnchanged;

    protected AssemblyReference(string cacheKey, Action validateUnchanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(validateUnchanged);
        CacheKey = cacheKey;
        _validateUnchanged = validateUnchanged;
    }

    /// <summary>Provider-defined identity that distinguishes copies and revisions in shared caches.</summary>
    public string CacheKey { get; }

    /// <summary>
    /// Asks the client to verify that the reference still identifies the selected assembly.
    /// Throws if it is no longer usable. Does not load the assembly.
    /// </summary>
    public void ValidateUnchanged() => _validateUnchanged();
}
