// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Core.Abstractions;

/// <summary>
/// Runtime-only handle to an installation and its owning loader. Loading never
/// searches for another installation by name or version.
/// </summary>
public sealed class ExtensionInstallation
{
    private readonly Func<Assembly> _load;
    private readonly Action _validateUnchanged;

    public ExtensionInstallation(
        string name,
        string version,
        string? location,
        string source,
        int sourcePriority,
        int rootPriority,
        string cacheKey,
        Func<Assembly> load,
        Action validateUnchanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(validateUnchanged);
        Name = name;
        Version = version;
        Location = location;
        Source = source;
        SourcePriority = sourcePriority;
        RootPriority = rootPriority;
        CacheKey = cacheKey;
        _load = load;
        _validateUnchanged = validateUnchanged;
    }

    public string Name { get; }
    public string Version { get; }
    public string? Location { get; }
    public string Source { get; }
    public int SourcePriority { get; }
    public int RootPriority { get; }

    /// <summary>Opaque owner/locator/generation identity, stable across unchanged enumeration.</summary>
    public string CacheKey { get; }

    public void ValidateUnchanged() => _validateUnchanged();

    public Assembly Load()
    {
        ValidateUnchanged();
        var assembly = _load();
        ValidateUnchanged();
        return assembly;
    }
}
