// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Core.Abstractions;

/// <summary>
/// Runtime-only handle to an installation and its owning loader. Loading never
/// searches for another installation by name or version.
/// </summary>
/// <remarks>
/// Handles belong to the provider instance that enumerated them and must not be persisted.
/// CLI filesystem handles capture primary DLL length and last-write UTC time; a missing
/// or changed DLL invalidates the handle, including previously cached assemblies and indexes.
/// This detects ordinary replacement, not byte identity or dependency-only changes.
/// </remarks>
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
    /// <summary>Lower values win equivalent-version ties; CLI Bundled precedes Installed.</summary>
    public int SourcePriority { get; }
    /// <summary>Lower values prefer an earlier root within the same source tier.</summary>
    public int RootPriority { get; }

    /// <summary>Opaque owner/locator/generation identity, stable across unchanged enumeration.</summary>
    public string CacheKey { get; }

    /// <summary>
    /// Validates the captured installation generation without loading its assembly.
    /// Call before reusing an index obtained from this handle.
    /// </summary>
    /// <exception cref="FileNotFoundException">The selected primary DLL was removed.</exception>
    /// <exception cref="InvalidOperationException">The selected primary DLL changed.</exception>
    public void ValidateUnchanged() => _validateUnchanged();

    /// <summary>Loads through the owning provider, validating before and after the load.</summary>
    public Assembly Load()
    {
        ValidateUnchanged();
        var assembly = _load();
        ValidateUnchanged();
        return assembly;
    }
}
