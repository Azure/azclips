using System.Runtime.InteropServices;
using System.Text;

namespace Core.Agent.Hosting;

/// <summary>
/// Reads one line with the platform's own line editing while letting the session's
/// cancellation end the wait.
/// </summary>
/// <remarks>
/// A blocked read cannot be interrupted on Windows or Linux, so the read runs on a background
/// task and the caller awaits it together with the token, the same way System.CommandLine's
/// InvocationPipeline races the command against process termination. After cancellation the
/// abandoned read stays blocked until the process exits, so nothing may touch stdin afterwards:
/// on Linux it holds the stdin reader lock, and a later <see cref="Console.KeyAvailable"/> or
/// <see cref="Console.ReadKey(bool)"/> would deadlock.
/// </remarks>
internal static class ConsoleLineInput
{
    /// <summary>Returns the line, or null at end of input.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled before a line arrived.</exception>
    public static Task<string?> ReadLineAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.Run(Console.ReadLine, CancellationToken.None).WaitAsync(ct);
    }

    /// <summary>
    /// Like <see cref="ReadLineAsync"/>, but on a Windows console Ctrl+C is not taken as end of input.
    /// </summary>
    public static Task<string?> ReadLineOrCancelAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected) return ReadLineAsync(ct);
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => ReadWindowsConsoleLine(ct), CancellationToken.None).WaitAsync(ct);
    }

    internal enum ReadKind { Line, EndOfInput, Interrupted, Error }

    /// <summary>
    /// Classifies one ReadConsoleW result the way CPython's _PyOS_WindowsConsoleReadline does:
    /// 0 characters with ERROR_OPERATION_ABORTED is Ctrl+C, a line starting with Ctrl+Z is end of input.
    /// </summary>
    internal static ReadKind Classify(bool ok, int charsRead, int error, char first)
    {
        if (!ok) return ReadKind.Error;
        if (charsRead == 0) return error == ErrorOperationAborted ? ReadKind.Interrupted : ReadKind.EndOfInput;
        return first == '\x1a' ? ReadKind.EndOfInput : ReadKind.Line;
    }

    // Follows CPython Parser/myreadline.c _PyOS_WindowsConsoleReadline: call ReadConsoleW until
    // the buffer ends with a newline. 0 chars with ERROR_OPERATION_ABORTED (995) is Ctrl+C: wait
    // up to 100 ms for the Ctrl+C handler (here it cancels the session token) and raise if it ran;
    // otherwise return what was read, as CPython breaks out of its loop. A first char of '\x1a'
    // (Ctrl+Z) is EOF. Console.In.ReadLine cannot tell these apart.
    private static string? ReadWindowsConsoleLine(CancellationToken ct)
    {
        var handle = GetStdHandle(StdInputHandle);
        var chunk = new char[1024];
        var line = new StringBuilder();
        while (true)
        {
            Marshal.SetLastPInvokeError(0);
            var ok = ReadConsoleW(handle, chunk, chunk.Length, out var read, IntPtr.Zero);
            var error = Marshal.GetLastPInvokeError();
            var first = line.Length > 0 ? line[0] : read > 0 ? chunk[0] : '\0';
            var kind = Classify(ok, line.Length + read, error, first);
            if (kind == ReadKind.Interrupted)
            {
                if (ct.WaitHandle.WaitOne(InterruptWait)) throw new OperationCanceledException(ct);
                break;
            }
            if (kind == ReadKind.EndOfInput) return null;
            if (kind == ReadKind.Error) throw new IOException($"ReadConsoleW failed with error {error}.");
            line.Append(chunk, 0, read);
            if (read > 0 && chunk[read - 1] == '\n') break;
        }
        var length = line.Length;
        if (length > 0 && line[length - 1] == '\n') length--;
        if (length > 0 && line[length - 1] == '\r') length--;
        return line.ToString(0, length);
    }

    private const int StdInputHandle = -10;
    private const int ErrorOperationAborted = 995;
    private static readonly TimeSpan InterruptWait = TimeSpan.FromMilliseconds(100);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadConsoleW(IntPtr hConsoleInput, [Out] char[] lpBuffer, int nNumberOfCharsToRead, out int lpNumberOfCharsRead, IntPtr pInputControl);
}
