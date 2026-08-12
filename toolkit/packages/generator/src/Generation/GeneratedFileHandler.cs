namespace Ace.Generator;

/// <summary>
/// Receives one generated file the moment it is produced, returning a task the
/// generator awaits before producing the next (so a slow sink applies natural
/// backpressure and nothing is buffered).
///
/// <paramref name="relativePath"/> is <em>relative</em> to the caller's output
/// base directory (using <c>/</c> separators, never absolute, never escaping via
/// <c>..</c>); the generator is filesystem-agnostic and never knows the base
/// directory — the handler owns it (e.g. a filesystem writer that joins
/// <c>baseDir + relativePath</c>). So the whole generated output is one
/// relocatable folder tree.
///
/// This is the C# counterpart to the TypeScript backbone's <c>OutputDelegate</c>:
/// the generator decides <em>when</em> each artifact is emitted; the handler
/// decides <em>where/how</em> it lands (an orchestrator writing to disk, or a test
/// collecting in memory). A synchronous sink can simply return a completed task.
/// </summary>
/// <param name="relativePath">Output path relative to the caller's base directory.</param>
/// <param name="contents">The generated file's full text.</param>
/// <param name="cancellationToken">Cancellation for the write.</param>
public delegate ValueTask GeneratedFileHandler(
    string relativePath,
    string contents,
    CancellationToken cancellationToken);


