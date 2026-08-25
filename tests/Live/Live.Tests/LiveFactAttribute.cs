using System.Runtime.CompilerServices;

namespace Live.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips unless the live-test environment is
/// configured. Mirrors the <c>SkipOnWindowsFactAttribute</c> pattern already used
/// in <c>Cli.Tests</c>: the skip decision is data, not an exception, so an
/// unconfigured run reports "skipped" instead of failing.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveFactAttribute : FactAttribute
{
    // The caller-info parameters are forwarded to the base so the test explorer can
    // navigate to the test; that rules out a `params` constructor for the required
    // variables, hence the settable property below.
    public LiveFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = LiveTestEnvironment.GetSkipReason();
    }

    /// <summary>
    /// Extra environment variables this specific case needs on top of the common
    /// ones (for example the user-assigned identity selector it exercises).
    /// Attribute properties are assigned after the constructor runs, so this
    /// recomputes <see cref="FactAttribute.Skip"/>.
    /// </summary>
    public string[] RequiresVariables
    {
        get;
        set
        {
            field = value;
            Skip = LiveTestEnvironment.GetSkipReason(value);
        }
    } = [];
}
