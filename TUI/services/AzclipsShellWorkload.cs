using System.Text;
using System.Threading.Channels;
using Core.Tui.Rendering;
using Hex1b;

namespace Core.Tui.Services;

/// <summary>
/// A custom shell workload that manages prompt rendering, line editing, and command dispatch.
/// Prompt is rendered as ANSI output (read-only). User input is captured as raw bytes.
/// The cursor naturally stays in the editable region — no reflection hacks needed.
/// </summary>
sealed class CustomShellWorkload : IHex1bTerminalWorkloadAdapter
{
    private readonly AppState _state;
    private readonly Channel<byte[]> _outputChannel = Channel.CreateUnbounded<byte[]>();
    private readonly StringBuilder _inputBuffer = new();
    private int _cursorPos; // cursor position within _inputBuffer (not including prompt)
    private int _width;
    private int _height;
    private int _lastRows = 1; // last rendered row count for resize detection

    // ANSI escape sequences
    private const string Csi = "\x1b[";
    private const string Reset = "\x1b[0m";
    private const string ClearLine = "\x1b[2K";
    private const string CursorToCol1 = "\x1b[1G"; // move cursor to column 1

    public CustomShellWorkload(AppState state, int width = 80, int height = 3)
    {
        _state = state;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Number of rows needed to display the prompt + current input.
    /// </summary>
    public int RequiredRows
    {
        get
        {
            if (_width <= 0) return 1;
            var totalLen = _state.Prompt.Length + _inputBuffer.Length;
            return Math.Max(1, (totalLen + _width - 1) / _width);
        }
    }

    /// <summary>
    /// Writes bytes to the output channel for the terminal to display.
    /// </summary>
    public void WriteOutput(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _outputChannel.Writer.TryWrite(bytes);
    }

    /// <summary>
    /// Renders the current prompt + input buffer + cursor position.
    /// </summary>
    public void RedrawPromptLine()
    {
        var prompt = BuildColoredPrompt();
        var input = _inputBuffer.ToString();
        var promptLen = _state.Prompt.Length; // plain text length for cursor math

        // Check if row count changed — need to resize the terminal widget
        var rows = RequiredRows;
        if (rows != _lastRows)
        {
            _lastRows = rows;
            _state.Invalidate?.Invoke();
        }

        // Move cursor to top-left of the terminal, clear screen, write prompt + input
        var sb = new StringBuilder();
        sb.Append("\x1b[H");     // cursor to row 1, col 1
        sb.Append("\x1b[J");     // clear from cursor to end of screen
        sb.Append(prompt);
        sb.Append(input);

        // Position cursor: calculate row and column from linear offset
        var cursorOffset = promptLen + _cursorPos; // 0-based offset in plain text
        if (_width > 0)
        {
            var row = cursorOffset / _width + 1;   // 1-based row
            var col = cursorOffset % _width + 1;   // 1-based column
            sb.Append($"{Csi}{row};{col}H");
        }

        WriteOutput(sb.ToString());
    }

    /// <summary>
    /// Shows the initial prompt.
    /// </summary>
    public void Start()
    {
        _inputBuffer.Clear();
        _cursorPos = 0;
        RedrawPromptLine();
    }

    /// <summary>
    /// Process raw input bytes from the terminal.
    /// Handles line editing (backspace, delete, arrows, home, end) and command submission (Enter).
    /// </summary>
    public void HandleInput(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        int i = 0;
        while (i < bytes.Length)
        {
            // ESC sequence
            if (bytes[i] == 0x1b && i + 1 < bytes.Length && bytes[i + 1] == '[')
            {
                i += 2;
                // Parse CSI parameters
                var paramStart = i;
                while (i < bytes.Length && bytes[i] >= 0x30 && bytes[i] <= 0x3F) i++;
                var code = i < bytes.Length ? (char)bytes[i] : '\0';
                i++;

                switch (code)
                {
                    case 'A': // Up arrow — history
                        HandleUpArrow();
                        break;
                    case 'B': // Down arrow — history
                        HandleDownArrow();
                        break;
                    case 'C': // Right arrow
                        if (_cursorPos < _inputBuffer.Length) _cursorPos++;
                        RedrawPromptLine();
                        break;
                    case 'D': // Left arrow
                        if (_cursorPos > 0) _cursorPos--;
                        RedrawPromptLine();
                        break;
                    case 'H': // Home
                        _cursorPos = 0;
                        RedrawPromptLine();
                        break;
                    case 'F': // End
                        _cursorPos = _inputBuffer.Length;
                        RedrawPromptLine();
                        break;
                    case '~': // Delete (CSI 3 ~)
                        var param = Encoding.ASCII.GetString(bytes[paramStart..i]);
                        if (param.StartsWith("3") && _cursorPos < _inputBuffer.Length)
                        {
                            _inputBuffer.Remove(_cursorPos, 1);
                            _state.InputText = _inputBuffer.ToString();
                            _state.UpdateSuggestions();
                            RedrawPromptLine();
                        }
                        break;
                    case 'Z': // Shift+Tab — ignore
                        break;
                }
                continue;
            }

            var b = bytes[i];
            i++;

            switch (b)
            {
                case 0x0D or 0x0A: // Enter
                    HandleEnter();
                    break;
                case 0x7F or 0x08: // Backspace
                    if (_cursorPos > 0)
                    {
                        _cursorPos--;
                        _inputBuffer.Remove(_cursorPos, 1);
                        _state.InputText = _inputBuffer.ToString();
                        _state.UpdateSuggestions();
                        RedrawPromptLine();
                    }
                    break;
                case 0x09: // Tab — accept suggestion
                    HandleTab();
                    break;
                case 0x01: // Ctrl+A — Home
                    _cursorPos = 0;
                    RedrawPromptLine();
                    break;
                case 0x05: // Ctrl+E — End
                    _cursorPos = _inputBuffer.Length;
                    RedrawPromptLine();
                    break;
                case 0x0B: // Ctrl+K — delete to end of line
                    if (_cursorPos < _inputBuffer.Length)
                    {
                        _inputBuffer.Remove(_cursorPos, _inputBuffer.Length - _cursorPos);
                        _state.InputText = _inputBuffer.ToString();
                        _state.UpdateSuggestions();
                        RedrawPromptLine();
                    }
                    break;
                case 0x15: // Ctrl+U — delete to start of line
                    if (_cursorPos > 0)
                    {
                        _inputBuffer.Remove(0, _cursorPos);
                        _cursorPos = 0;
                        _state.InputText = _inputBuffer.ToString();
                        _state.UpdateSuggestions();
                        RedrawPromptLine();
                    }
                    break;
                case 0x17: // Ctrl+W — delete word backward
                    HandleDeleteWordBackward();
                    break;
                case 0x0C: // Ctrl+L — clear history
                    _state.ClearHistory();
                    _state.OutputScrollOffset = 0;
                    _state.Invalidate?.Invoke();
                    break;
                case 0x03: // Ctrl+C — cancel / clear input
                    _inputBuffer.Clear();
                    _cursorPos = 0;
                    _state.InputText = "";
                    _state.UpdateSuggestions();
                    RedrawPromptLine();
                    break;
                default:
                    // Printable character or UTF-8 multi-byte
                    if (b >= 0x20)
                    {
                        // Handle UTF-8 multi-byte sequences
                        var charBytes = DecodeUtf8Char(bytes, i - 1, out var ch, out var consumed);
                        if (ch != null)
                        {
                            _inputBuffer.Insert(_cursorPos, ch);
                            _cursorPos += ch.Length;
                            _state.InputText = _inputBuffer.ToString();
                            _state.UpdateSuggestions();
                            RedrawPromptLine();
                            i = i - 1 + consumed; // adjust index for multi-byte
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Refreshes the prompt display (e.g., after VFS path change).
    /// </summary>
    public void RefreshPrompt()
    {
        _state.RefreshPrompt();
        RedrawPromptLine();
    }

    /// <summary>
    /// Sets the input text and redraws (used by history navigation, suggestion acceptance).
    /// </summary>
    public void SetInput(string text)
    {
        _inputBuffer.Clear();
        _inputBuffer.Append(text);
        _cursorPos = _inputBuffer.Length;
        _state.InputText = text;
        _state.UpdateSuggestions();
        RedrawPromptLine();
    }

    // ── IHex1bTerminalWorkloadAdapter implementation ──

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        var bytes = await _outputChannel.Reader.ReadAsync(ct);
        return new ReadOnlyMemory<byte>(bytes);
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        HandleInput(data);
        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        _width = width;
        _height = height;
        RedrawPromptLine();
        return ValueTask.CompletedTask;
    }

    public event Action? Disconnected;

    public ValueTask DisposeAsync()
    {
        _outputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }

    // ── Private helpers ──

    private string BuildColoredPrompt()
    {
        var prompt = _state.Prompt;
        // Color based on mode: blue for Azure, white for Local
        var color = _state.Mode == ShellMode.Azure ? Ansi.AccentBlue : Ansi.White;
        if (prompt.EndsWith(" > "))
        {
            var path = prompt[..^3];
            return $"{color}{path}{Reset} {color}>{Reset} ";
        }
        return $"{color}{prompt}{Reset}";
    }

    private void HandleEnter()
    {
        // Move to new line before executing
        WriteOutput("\r\n");
        _state.ExecuteCommand();
        _inputBuffer.Clear();
        _cursorPos = 0;
        // Prompt will be redrawn after command completes via RefreshPrompt
        RedrawPromptLine();
    }

    private void HandleTab()
    {
        _state.AcceptSuggestion();
        _inputBuffer.Clear();
        _inputBuffer.Append(_state.InputText);
        _cursorPos = _inputBuffer.Length;
        RedrawPromptLine();
    }

    private void HandleUpArrow()
    {
        if (_state.Suggestions.Count > 0)
        {
            _state.NavigateSuggestion(-1);
        }
        else
        {
            _state.NavigateHistory(-1);
            _inputBuffer.Clear();
            _inputBuffer.Append(_state.InputText);
            _cursorPos = _inputBuffer.Length;
        }
        _state.Invalidate?.Invoke();
        RedrawPromptLine();
    }

    private void HandleDownArrow()
    {
        if (_state.Suggestions.Count > 0)
        {
            _state.NavigateSuggestion(1);
        }
        else
        {
            _state.NavigateHistory(1);
            _inputBuffer.Clear();
            _inputBuffer.Append(_state.InputText);
            _cursorPos = _inputBuffer.Length;
        }
        _state.Invalidate?.Invoke();
        RedrawPromptLine();
    }

    private void HandleDeleteWordBackward()
    {
        if (_cursorPos == 0) return;
        int end = _cursorPos;
        // Skip trailing spaces
        while (_cursorPos > 0 && _inputBuffer[_cursorPos - 1] == ' ') _cursorPos--;
        // Skip word characters
        while (_cursorPos > 0 && _inputBuffer[_cursorPos - 1] != ' ') _cursorPos--;
        _inputBuffer.Remove(_cursorPos, end - _cursorPos);
        _state.InputText = _inputBuffer.ToString();
        _state.UpdateSuggestions();
        RedrawPromptLine();
    }

    private static string? DecodeUtf8Char(ReadOnlySpan<byte> bytes, int start, out string? ch, out int consumed)
    {
        ch = null;
        consumed = 1;
        if (start >= bytes.Length) return null;

        var b = bytes[start];
        int expected;
        if (b < 0x80) { expected = 1; }
        else if ((b & 0xE0) == 0xC0) { expected = 2; }
        else if ((b & 0xF0) == 0xE0) { expected = 3; }
        else if ((b & 0xF8) == 0xF0) { expected = 4; }
        else return null;

        if (start + expected > bytes.Length) return null;
        consumed = expected;
        ch = Encoding.UTF8.GetString(bytes.Slice(start, expected));
        return ch;
    }
}
