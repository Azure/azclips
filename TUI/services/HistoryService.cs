namespace Core.Tui.Services;

/// <summary>
/// Manages dual-mode (Azure/Local) command history — load, save, navigate.
/// </summary>
class HistoryService
{
    private readonly string _directory;
    private readonly string _azurePath;
    private readonly string _localPath;

    /// <param name="directory">History directory; defaults to {LocalApplicationData}/.azclips.</param>
    public HistoryService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".azclips");
        _azurePath = Path.Combine(_directory, "tui_history_azure.txt");
        _localPath = Path.Combine(_directory, "tui_history_local.txt");
    }

    private readonly List<string> _azureHistory = new();
    private readonly List<string> _localHistory = new();
    private int _historyIndex = -1;

    public List<string> GetHistory(ShellMode mode) =>
        mode == ShellMode.Azure ? _azureHistory : _localHistory;

    /// <summary>
    /// Load both history files from disk.
    /// </summary>
    public void Load()
    {
        LoadFile(_azurePath, _azureHistory);
        LoadFile(_localPath, _localHistory);
    }

    /// <summary>
    /// Record a command in the current mode's history and persist to disk.
    /// </summary>
    public void Record(string command, ShellMode mode)
    {
        GetHistory(mode).Add(command);
        _historyIndex = -1;
        _ = SaveAsync(command, mode);
    }

    /// <summary>
    /// Navigate through history. direction: -1 = older, +1 = newer.
    /// Returns the command to display, or null if no change.
    /// </summary>
    public string? Navigate(int direction, ShellMode mode)
    {
        var history = GetHistory(mode);
        if (history.Count == 0) return null;

        if (direction < 0) // older
        {
            if (_historyIndex < history.Count - 1)
            {
                _historyIndex++;
                return history[history.Count - 1 - _historyIndex];
            }
        }
        else // newer
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                return history[history.Count - 1 - _historyIndex];
            }
            if (_historyIndex == 0)
            {
                _historyIndex = -1;
                return ""; // clear input
            }
        }

        return null;
    }

    private static void LoadFile(string path, List<string> target)
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    target.Add(trimmed);
            }
        }
        catch { /* silently ignore */ }
    }

    private async Task SaveAsync(string command, ShellMode mode)
    {
        try
        {
            var path = mode == ShellMode.Azure ? _azurePath : _localPath;
            Directory.CreateDirectory(_directory);
            await File.AppendAllTextAsync(path, command + Environment.NewLine);
        }
        catch { /* silently ignore */ }
    }
}
