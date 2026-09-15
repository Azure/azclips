namespace Ace.Generator.Model;

/// <summary>
/// One generated artifact: a <see cref="Path"/> relative to the caller's output
/// base directory, the file <see cref="Contents"/>, and (for a file that declares
/// a type) its <see cref="ClassName"/>. Produced lazily by the artifact generators
/// and streamed to the caller's handler by the orchestrator.
/// </summary>
/// <param name="Path">
/// Path <em>relative</em> to the output base directory, using <c>/</c> separators.
/// Never absolute and never escaping the base directory (no <c>..</c>); the
/// orchestrator validates this before streaming.
/// </param>
/// <param name="Contents">The generated file's full text.</param>
/// <param name="ClassName">
/// The primary C# type declared in the file, or <c>null</c> for non-type
/// artifacts (e.g. the project file). The orchestrator records
/// <c>ClassName -&gt; Path</c> on the result.
/// </param>
internal readonly record struct GeneratedFile(string Path, string Contents, string? ClassName = null);

