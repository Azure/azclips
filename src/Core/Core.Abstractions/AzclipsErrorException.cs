// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Base exception for structured AzClips CLI errors. Carries a symbolic <see cref="Code"/> for
/// programmatic callers and a numeric <see cref="ExitCode"/> for shell scripts.
/// </summary>
/// <remarks>
/// Exit codes:
/// <list type="bullet">
///   <item>0 success</item>
///   <item>1 generic error</item>
///   <item>2 usage error (<see cref="UsageError"/>)</item>
///   <item>3 verb / leaf conflict (<see cref="VerbConflict"/>, <see cref="LeafConflict"/>)</item>
///   <item>4 extension not found (<see cref="ExtensionNotFound"/>)</item>
///   <item>5 feed / network unavailable (<see cref="FeedUnavailable"/>)</item>
///   <item>6 pin violation (<see cref="PinViolation"/>)</item>
///   <item>7 compatibility violation (<see cref="CompatibilityViolation"/>)</item>
///   <item>8 registry concurrency conflict (<see cref="RegistryConcurrency"/>)</item>
///   <item>9 registry invariant violation (<see cref="RegistryInvalid"/>)</item>
///   <item>JSON shape: <c>{ "error": { "code": "...", "message": "...", "details": { ... } } }</c>.</item>
/// </list>
/// </remarks>
public class AzclipsErrorException : Exception
{
    public string Code { get; }
    public int ExitCode { get; }
    public IReadOnlyDictionary<string, object?> Details { get; }

    public AzclipsErrorException(string code, int exitCode, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
        Details = details ?? new Dictionary<string, object?>();
    }

    public static AzclipsErrorException VerbConflict(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("VerbConflict", 3, message, details);

    /// <summary>
    /// Leaf-level conflict: two extensions claim the same <c>(verb, path)</c> tuple, or an
    /// extension claims a leaf already provided by a built-in. Shares the same exit-code lane
    /// as <see cref="VerbConflict"/> since both express "namespace collision".
    /// </summary>
    public static AzclipsErrorException LeafConflict(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("LeafConflict", 3, message, details);

    public static AzclipsErrorException ExtensionNotFound(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("ExtensionNotFound", 4, message, details);

    public static AzclipsErrorException PinViolation(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("PinViolation", 6, message, details);

    public static AzclipsErrorException FeedUnavailable(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("FeedUnavailable", 5, message, details);

    public static UsageErrorException UsageError(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(message, details);

    public static UsageErrorException MissingRequiredParameters(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(message, details, hasMissingRequiredParameters: true);

    /// <summary>
    /// Compatibility violation: the extension's <c>[AzclipsCompatibility]</c> declared range
    /// excludes the current core version, or the host requires a newer version of this
    /// specific extension via <c>HostExtensionRequirements</c>. Surfaced at install time as a
    /// hard error with no disk write; surfaced at load time as a warn-and-skip log line.
    /// </summary>
    public static AzclipsErrorException CompatibilityViolation(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("CompatibilityViolation", 7, message, details);

    /// <summary>
    /// Optimistic-concurrency failure on <c>registry.json</c>: the file changed between this
    /// process's <c>Load</c> and <c>Save</c>. Surfaced so the caller can retry or
    /// inform the user instead of silently overwriting another process's changes.
    /// </summary>
    public static AzclipsErrorException RegistryConcurrency(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("RegistryConcurrency", 8, message, details);

    /// <summary>
    /// Invariant violation discovered while loading <c>registry.json</c> (e.g. two entries claim
    /// the same <c>(verb, path)</c> contribution). Distinct from <see cref="VerbConflict"/> /
    /// <see cref="LeafConflict"/> which fire at install time; this one fires on an already-bad
    /// on-disk file (registry reconciler).
    /// </summary>
    public static AzclipsErrorException RegistryInvalid(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("RegistryInvalid", 9, message, details);
}

public class UsageErrorException : AzclipsErrorException
{
    public bool HasMissingRequiredParameters { get; }

    public UsageErrorException(string message, IReadOnlyDictionary<string, object?>? details = null, bool hasMissingRequiredParameters = false)
        : base("UsageError", 2, message, details)
    {
        HasMissingRequiredParameters = hasMissingRequiredParameters;
    }
}
