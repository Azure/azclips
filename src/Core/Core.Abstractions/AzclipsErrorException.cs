// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Base exception for known AzClips CLI errors. Carries a symbolic <see cref="Code"/> for
/// classification and telemetry and a numeric <see cref="ExitCode"/> for shell scripts.
/// </summary>
/// <remarks>
/// Add a focused subtype when an error category changes host behavior such as help rendering,
/// recovery, exit handling, or telemetry. Errors that differ only in wording can share a type.
///
/// Exit codes:
/// <list type="bullet">
///   <item>0 success</item>
///   <item>1 generic error</item>
///   <item>2 usage error (<see cref="UsageError"/>) or invalid input (<see cref="InvalidInputException"/>)</item>
///   <item>3 verb / leaf conflict (<see cref="VerbConflict"/>, <see cref="LeafConflict"/>)</item>
///   <item>4 extension not found (<see cref="ExtensionNotFound"/>)</item>
///   <item>5 feed / network unavailable (<see cref="FeedUnavailable"/>)</item>
///   <item>6 pin violation (<see cref="PinViolation"/>)</item>
///   <item>7 compatibility violation (<see cref="CompatibilityViolation"/>)</item>
///   <item>8 registry concurrency conflict (<see cref="RegistryConcurrency"/>)</item>
///   <item>9 registry invariant violation (<see cref="RegistryInvalid"/>)</item>
/// </list>
/// </remarks>
public class AzclipsErrorException : Exception
{
    public string Code { get; }
    public int ExitCode { get; }
    public IReadOnlyDictionary<string, object?> Details { get; }

    /// <summary>
    /// When true, the CLI offers the error to the AI agent before printing it. Only subtypes that
    /// represent recoverable input mistakes set this.
    /// </summary>
    public virtual bool AgentFallbackEligible => false;

    public AzclipsErrorException(string code, int exitCode, string message, IReadOnlyDictionary<string, object?>? details = null, Exception? innerException = null)
        : base(message, innerException)
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

    /// <summary>
    /// A named profile does not exist. Uses the generic exit code (1); the symbolic code lets scripts
    /// and telemetry distinguish it from other failures.
    /// </summary>
    public static AzclipsErrorException ProfileNotFound(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("ProfileNotFound", 1, message, details);

    /// <summary>
    /// The command needs an active profile or credential that is missing, such as no active
    /// profile, no federated token, or no subscription in the profile. The fix is to sign in.
    /// This is not caused by the user's input, so the agent is not offered the error.
    /// </summary>
    public static AzclipsErrorException NotSignedIn(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("NotSignedIn", 1, message, details);

    public static AzclipsErrorException PinViolation(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("PinViolation", 6, message, details);

    public static AzclipsErrorException FeedUnavailable(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("FeedUnavailable", 5, message, details);

    // Declared return type is the base so already-compiled callers keep binding; the runtime type is the subtype.
    public static AzclipsErrorException UsageError(string message, IReadOnlyDictionary<string, object?>? details = null, Exception? innerException = null)
        => new UsageErrorException(message, details, innerException: innerException);

    public static UsageErrorException MissingRequiredParameters(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new MissingRequiredArgumentException(message, details);

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

    /// <summary>
    /// A user-supplied value is malformed or out of range (for example a bad <c>--set</c>
    /// expression or an unrecognized configuration key). Offered to the AI agent before it is printed.
    /// </summary>
    public static InvalidInputException InvalidInput(string message, IReadOnlyDictionary<string, object?>? details = null, Exception? innerException = null)
        => new(message, details, innerException);
}

/// <summary>
/// A user-supplied value is malformed. Exits with code 2, the same lane as <see cref="UsageErrorException"/>,
/// and is eligible for AI agent fallback.
/// </summary>
public sealed class InvalidInputException : AzclipsErrorException
{
    public InvalidInputException(string message, IReadOnlyDictionary<string, object?>? details = null, Exception? innerException = null)
        : base("InvalidInput", 2, message, details, innerException)
    {
    }

    public override bool AgentFallbackEligible => true;
}

public class UsageErrorException : AzclipsErrorException
{
    // Retained for compatibility with callers compiled before missing arguments had a dedicated subtype.
    public bool HasMissingRequiredParameters { get; }

    public UsageErrorException(string message, IReadOnlyDictionary<string, object?>? details = null, bool hasMissingRequiredParameters = false, Exception? innerException = null)
        : base("UsageError", 2, message, details, innerException)
    {
        HasMissingRequiredParameters = hasMissingRequiredParameters;
    }
}

/// <summary>
/// A resolved command invocation is missing one or more required arguments.
/// </summary>
public sealed class MissingRequiredArgumentException : UsageErrorException
{
    public MissingRequiredArgumentException(
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message, details, hasMissingRequiredParameters: true)
    {
    }
}
