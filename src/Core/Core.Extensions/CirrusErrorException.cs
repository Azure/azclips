namespace Core.Extensions;

/// <summary>
/// Base exception for structured Cirrus CLI errors. Carries a symbolic <see cref="Code"/> for
/// programmatic callers and a numeric <see cref="ExitCode"/> for shell scripts.
/// </summary>
/// <remarks>
/// Per <c>docs/spec/extension-management.md \u00a74.4</c>:
/// <list type="bullet">
///   <item>0 success, 1 generic error, 2 usage error, 3 verb conflict, 4 extension not found,
///   5 feed/network error, 6 pin violation.</item>
///   <item>JSON shape: <c>{ "error": { "code": "...", "message": "...", "details": { ... } } }</c>.</item>
/// </list>
/// </remarks>
public class CirrusErrorException : Exception
{
    public string Code { get; }
    public int ExitCode { get; }
    public IReadOnlyDictionary<string, object?> Details { get; }

    public CirrusErrorException(string code, int exitCode, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
        Details = details ?? new Dictionary<string, object?>();
    }

    public static CirrusErrorException VerbConflict(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("VerbConflict", 3, message, details);

    /// <summary>
    /// Leaf-level conflict (Shape B §11.7): two extensions claim the same <c>(verb, path)</c>
    /// tuple, or an extension claims a leaf already provided by a built-in. Shares the same
    /// exit-code lane as <see cref="VerbConflict"/> since both express "namespace collision".
    /// </summary>
    public static CirrusErrorException LeafConflict(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("LeafConflict", 3, message, details);

    public static CirrusErrorException ExtensionNotFound(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("ExtensionNotFound", 4, message, details);

    public static CirrusErrorException PinViolation(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("PinViolation", 6, message, details);

    public static CirrusErrorException FeedUnavailable(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("FeedUnavailable", 5, message, details);

    public static CirrusErrorException UsageError(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("UsageError", 2, message, details);

    /// <summary>
    /// Optimistic-concurrency failure on <c>registry.json</c>: the file changed between this
    /// process's <c>Load</c> and <c>Save</c> (Phase 8.5). Surfaced so the caller can retry or
    /// inform the user instead of silently overwriting another process's changes.
    /// </summary>
    public static CirrusErrorException RegistryConcurrency(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("RegistryConcurrency", 7, message, details);

    /// <summary>
    /// Invariant violation discovered while loading <c>registry.json</c> (e.g. two entries claim
    /// the same <c>(verb, path)</c> contribution). Distinct from <see cref="VerbConflict"/> /
    /// <see cref="LeafConflict"/> which fire at install time; this one fires on an already-bad
    /// on-disk file (Phase 8.5).
    /// </summary>
    public static CirrusErrorException RegistryInvalid(string message, IReadOnlyDictionary<string, object?>? details = null)
        => new("RegistryInvalid", 8, message, details);
}
