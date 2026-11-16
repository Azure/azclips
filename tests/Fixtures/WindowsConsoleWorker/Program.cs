// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Cli.Tests.WindowsConsoleWorker;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Length != 2 || !uint.TryParse(args[1], out var initialMode))
        {
            Console.Error.WriteLine("Usage (Windows only): WindowsConsole.TestWorker <az.exe> <initial-mode>");
            return 2;
        }

        try
        {
            Run(Path.GetFullPath(args[0]), initialMode);
            Console.WriteLine("PASS: VT enabled, flags preserved, heading bold, body normal, no literal escapes.");
            return 0;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or TimeoutException)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(string executable, uint initialMode)
    {
        var oldOutput = GetStdHandle(-11);
        var oldError = GetStdHandle(-12);
        // Only this worker detaches; the test runner keeps its console and standard handles.
        FreeConsole();
        Check(AllocConsole());
        var output = IntPtr.Zero;
        try
        {
            output = CreateFile("CONOUT$", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            Check(output != new IntPtr(-1));
            Check(SetHandleInformation(output, 1, 1));
            Check(SetConsoleMode(output, initialMode));
            Check(SetConsoleTextAttribute(output, 7));
            Environment.SetEnvironmentVariable("NO_COLOR", null);
            Environment.SetEnvironmentVariable("FORCE_COLOR", "1");

            var start = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfo>(),
                Flags = 0x100, // STARTF_USESTDHANDLES: attach CLI output to the real console, not a pipe.
                Input = GetStdHandle(-10),
                Output = output,
                Error = output,
            };
            Check(CreateProcess(executable, new StringBuilder($"\"{executable}\" config --help"),
                IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, null, ref start, out var process));
            try
            {
                var wait = WaitForSingleObject(process.Process, 60_000);
                if (wait != 0)
                {
                    using var child = Process.GetProcessById(process.ProcessId);
                    child.Kill(entireProcessTree: true);
                    child.WaitForExit();
                    throw new TimeoutException($"CLI did not exit successfully within 60 seconds (wait result {wait}).");
                }

                Check(GetExitCodeProcess(process.Process, out var exitCode));
                Require(exitCode == 0, $"CLI exited with code {exitCode}.");
            }
            finally
            {
                Check(CloseHandle(process.Thread));
                Check(CloseHandle(process.Process));
            }

            Check(GetConsoleMode(output, out var mode));
            Require(mode == (initialMode | 5), $"Expected console mode {initialMode | 5}, got {mode}.");
            var text = new StringBuilder(8000);
            Check(ReadConsoleOutputCharacter(output, text, 8000, new Coord(), out var read));
            var attributes = new ushort[read];
            Check(ReadConsoleOutputAttribute(output, attributes, read, new Coord(), out var attributesRead));
            Require(attributesRead == read, "Incomplete console attributes.");
            var rendered = text.ToString();
            // The console may scroll earlier sections out of its buffer on headless agents.
            var header = rendered.IndexOf("# GLOBAL ARGUMENTS", StringComparison.Ordinal);
            var body = rendered.IndexOf("--debug:", StringComparison.Ordinal);
            Require(header >= 0 && body >= 0, $"Help heading/body missing from console buffer: {rendered}");
            Require(!rendered.Contains("[1m") && !rendered.Contains("[0m"),
                "Literal ANSI escape sequences appeared in the console buffer.");
            Require((attributes[header] & 8) != 0, "Help heading is not bold/intense.");
            Require((attributes[body] & 8) == 0, "Bold formatting leaked into argument text.");
        }
        finally
        {
            if (output != IntPtr.Zero && output != new IntPtr(-1)) Check(CloseHandle(output));
            Check(FreeConsole());
            Check(SetStdHandle(-11, oldOutput));
            Check(SetStdHandle(-12, oldError));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord { public short X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public ushort Show, ReservedSize;
        public IntPtr ReservedPointer, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int id, IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleTextAttribute(IntPtr handle, ushort attributes);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity,
        IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string? directory,
        ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ReadConsoleOutputCharacter(IntPtr handle, StringBuilder text, uint length, Coord position, out uint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadConsoleOutputAttribute(IntPtr handle, [Out] ushort[] attributes, uint length, Coord position, out uint read);
}
