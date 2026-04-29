using System.Diagnostics;
using System.Text;

namespace Cirrus.Autocomplete.MenuHost;

/// <summary>
/// Terminal menu renderer ported from autocomp-dotnet.
///
/// Behavior:
/// - Saves cursor position on first display
/// - Renders the menu below the command line
/// - Up/Down (arrow keys, j/k) navigate
/// - Enter selects, Escape/Ctrl+C cancels
/// - Restores cursor and clears the menu on exit
/// </summary>
internal sealed class MenuRenderer
{
    private readonly IReadOnlyList<string> _displayLines;
    private readonly IReadOnlyList<string> _values;

    private FileStream? _ttyStream;
    private StreamWriter? _ttyWriter;
    private string? _originalSttySettings;
    private bool _cursorSaved;
    private int _selectedIndex;

    public MenuRenderer(IEnumerable<string> displayLines, IEnumerable<string> values)
    {
        _displayLines = displayLines.ToList();
        _values = values.ToList();

        if (_displayLines.Count != _values.Count)
        {
            throw new ArgumentException("Display lines and values must have the same length.");
        }
    }

    /// <summary>
    /// Runs the interactive menu loop.
    /// Returns the selected value, or null on cancel.
    /// </summary>
    public string? Run()
    {
        if (_values.Count == 0)
        {
            return null;
        }

        try
        {
            OpenTty();
            DisplayMenu();

            while (true)
            {
                ReadResult key = ReadKey();
                switch (key)
                {
                    case ReadResult.Up:
                        MoveSelection(-1);
                        break;

                    case ReadResult.Down:
                        MoveSelection(1);
                        break;

                    case ReadResult.Enter:
                        ClearMenu();
                        return _values[_selectedIndex];

                    case ReadResult.Cancel:
                        ClearMenu();
                        return null;
                }
            }
        }
        finally
        {
            CloseTty();
        }
    }

    private enum ReadResult
    {
        None,
        Up,
        Down,
        Enter,
        Cancel,
    }

    private void OpenTty()
    {
        _ttyStream = new FileStream("/dev/tty", FileMode.Open, FileAccess.ReadWrite);
        _ttyWriter = new StreamWriter(_ttyStream, Encoding.UTF8) { AutoFlush = true };

        _originalSttySettings = RunSttyAndCapture("-g");
        RunSttyNoOutput("raw -echo isig");
    }

    private void CloseTty()
    {
        if (!string.IsNullOrWhiteSpace(_originalSttySettings))
        {
            RunSttyNoOutput(_originalSttySettings!);
        }

        _ttyWriter?.Dispose();
        _ttyStream?.Dispose();
    }

    private void WriteTty(string text)
    {
        _ttyWriter?.Write(text);
    }

    private void SaveCursor()
    {
        WriteTty("\u001b7\u001b[s");
    }

    private void RestoreCursor()
    {
        WriteTty("\u001b[u\u001b8");
    }

    private void DisplayMenu()
    {
        if (!_cursorSaved)
        {
            SaveCursor();
            _cursorSaved = true;
        }

        RestoreCursor();
        WriteTty("\u001b[J");
        WriteTty("\r\n");
        WriteTty("Select:\r\n");

        for (int i = 0; i < _displayLines.Count; i++)
        {
            string line = _displayLines[i];
            WriteTty(i == _selectedIndex ? $"  > {line}\r\n" : $"    {line}\r\n");
        }

        RestoreCursor();
    }

    private void ClearMenu(bool preserveAnchor = false)
    {
        if (!_cursorSaved)
        {
            return;
        }

        RestoreCursor();
        WriteTty("\u001b[J");

        if (!preserveAnchor)
        {
            _cursorSaved = false;
        }
    }

    private void MoveSelection(int delta)
    {
        _selectedIndex = (_selectedIndex + delta + _values.Count) % _values.Count;
        ClearMenu(preserveAnchor: true);
        DisplayMenu();
    }

    private ReadResult ReadKey()
    {
        FileStream? stream = _ttyStream;
        if (stream == null)
        {
            return ReadResult.Cancel;
        }

        if (!TryReadByteExact(stream, out byte first))
        {
            return ReadResult.Cancel;
        }

        char ch = (char)first;

        if (ch == '\u001b')
        {
            List<byte> seq = ReadEscapeSequence(stream);
            if (seq.Count == 0)
            {
                return ReadResult.Cancel;
            }

            byte last = seq[^1];
            if (last == (byte)'A')
            {
                return ReadResult.Up;
            }
            if (last == (byte)'B')
            {
                return ReadResult.Down;
            }

            return ReadResult.None;
        }

        return ch switch
        {
            'k' => ReadResult.Up,
            'j' => ReadResult.Down,
            '\r' or '\n' => ReadResult.Enter,
            '\u0003' => ReadResult.Cancel,
            _ => ReadResult.None,
        };
    }

    private static List<byte> ReadEscapeSequence(FileStream stream)
    {
        var result = new List<byte>();

        if (!TryReadByteExact(stream, out byte first))
        {
            return result;
        }

        result.Add(first);

        if (first == (byte)'O')
        {
            if (TryReadByteExact(stream, out byte tail))
            {
                result.Add(tail);
            }
            return result;
        }

        if (first == (byte)'[')
        {
            for (int i = 0; i < 8; i++)
            {
                if (!TryReadByteExact(stream, out byte next))
                {
                    break;
                }

                result.Add(next);
                if (next == (byte)'A' || next == (byte)'B' || next == (byte)'C' || next == (byte)'D' || next == (byte)'~')
                {
                    break;
                }
            }
        }

        return result;
    }

    private static bool TryReadByteExact(FileStream stream, out byte value)
    {
        var buffer = new byte[1];
        int read = stream.Read(buffer, 0, 1);
        if (read == 0)
        {
            value = 0;
            return false;
        }

        value = buffer[0];
        return true;
    }

    private static string? RunSttyAndCapture(string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"stty {args} < /dev/tty\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            return null;
        }

        return process.StandardOutput.ReadToEnd().Trim();
    }

    private static void RunSttyNoOutput(string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"stty {args} < /dev/tty\"",
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        process.WaitForExit();
    }
}
