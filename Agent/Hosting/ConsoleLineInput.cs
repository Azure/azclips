using System.Runtime.InteropServices;
using System.Text;

namespace Core.Agent.Hosting;

/// <summary>Reads one ReadConsoleW chunk; returns false when the call failed.</summary>
internal delegate bool ReadConsoleChunk(char[] buffer, out int read, out int error);

/// <summary>
/// Reads one line with the platform's own line editing while letting the session's
/// cancellation end the wait.
/// </summary>
/// <remarks>
/// A blocked read cannot be interrupted, so the read runs on a background task and the
/// caller awaits it together with the token, as System.CommandLine's InvocationPipeline
/// races the command against process termination. An abandoned read stays blocked until
/// the process exits.
/// </remarks>
internal static class ConsoleLineInput
{
    internal static readonly TimeSpan InterruptWait = TimeSpan.FromMilliseconds(100);
    private const int StdInputHandle = -10;
    private const int ErrorOperationAborted = 995;

    /// <summary>Returns the line, or null at end of input.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled before a line arrived.</exception>
    public static Task<string?> ReadLineAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
            return Task.Run(() => ReadWindowsConsoleLine(ReadConsole, ct, TimeProvider.System), CancellationToken.None)
                .WaitAsync(ct);
        var input = Console.In;
        return Task.Run(input.ReadLine, CancellationToken.None).WaitAsync(ct);
    }

    internal enum ReadKind { Line, EndOfInput, Interrupted, Error }

    /// <summary>
    /// Classifies one ReadConsoleW chunk as CPython's _PyOS_WindowsConsoleReadline does:
    /// 0 characters with ERROR_OPERATION_ABORTED is Ctrl+C, 0 characters otherwise is end of
    /// input when no error is set.
    /// </summary>
    internal static ReadKind Classify(bool ok, int chunkLength, int error)
    {
        if (!ok) return ReadKind.Error;
        if (chunkLength > 0) return ReadKind.Line;
        if (error == ErrorOperationAborted) return ReadKind.Interrupted;
        return error == 0 ? ReadKind.EndOfInput : ReadKind.Error;
    }

    /// <summary>
    /// Follows CPython Parser/myreadline.c _PyOS_WindowsConsoleReadline: read until the line ends
    /// with a newline. On Ctrl+C (0 chars, error 995) wait up to 100 ms for the Ctrl+C handler to
    /// cancel <paramref name="ct"/>; if it did, throw, otherwise read again. A first char of
    /// Ctrl+Z is end of input.
    /// </summary>
    internal static string? ReadWindowsConsoleLine(ReadConsoleChunk read, CancellationToken ct, TimeProvider timeProvider)
    {
        var chunk = new char[1024];
        var line = new StringBuilder();
        while (true)
        {
            var ok = read(chunk, out var count, out var error);
            switch (Classify(ok, count, error))
            {
                case ReadKind.Interrupted:
                    WaitForCancel(ct, timeProvider);
                    ct.ThrowIfCancellationRequested();
                    continue;
                case ReadKind.EndOfInput:
                    return line.Length == 0 ? null : Trim(line);
                case ReadKind.Error:
                    throw new IOException($"ReadConsoleW failed with error {error}.");
            }
            if (line.Length == 0 && chunk[0] == '\x1a') return null;
            line.Append(chunk, 0, count);
            if (chunk[count - 1] == '\n') return Trim(line);
        }
    }

    private static void WaitForCancel(CancellationToken ct, TimeProvider timeProvider)
    {
        try { Task.Delay(InterruptWait, timeProvider, ct).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
    }

    private static string Trim(StringBuilder line)
    {
        var length = line.Length;
        if (length > 0 && line[length - 1] == '\n') length--;
        if (length > 0 && line[length - 1] == '\r') length--;
        return line.ToString(0, length);
    }

    private static bool ReadConsole(char[] buffer, out int read, out int error)
    {
        Marshal.SetLastPInvokeError(0);
        var ok = ReadConsoleW(GetStdHandle(StdInputHandle), buffer, buffer.Length, out read, IntPtr.Zero);
        error = Marshal.GetLastPInvokeError();
        return ok;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadConsoleW(IntPtr hConsoleInput, [Out] char[] lpBuffer, int nNumberOfCharsToRead, out int lpNumberOfCharsRead, IntPtr pInputControl);
}
