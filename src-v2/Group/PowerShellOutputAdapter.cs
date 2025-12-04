using System.Management.Automation;
using Core.Abstractions;

namespace Group;

/// <summary>
/// PowerShell implementation of IOutputAdapter using PSCmdlet.Host.UI
/// </summary>
public class PowerShellOutputAdapter : IOutputAdapter
{
    private readonly PSCmdlet _cmdlet;

    public PowerShellOutputAdapter(PSCmdlet cmdlet)
    {
        _cmdlet = cmdlet;
    }

    public bool IsInteractive => _cmdlet.Host.UI != null;

    public Task WriteLineAsync(string message)
    {
        _cmdlet.Host.UI.WriteLine(message);
        return Task.CompletedTask;
    }

    public Task WriteAsync(string message)
    {
        _cmdlet.Host.UI.Write(message);
        return Task.CompletedTask;
    }

    public Task<string?> ReadLineAsync(string prompt)
    {
        if (!string.IsNullOrEmpty(prompt))
        {
            _cmdlet.Host.UI.Write(ConsoleColor.White, _cmdlet.Host.UI.RawUI.BackgroundColor, prompt);
        }
        var input = _cmdlet.Host.UI.ReadLine();
        return Task.FromResult<string?>(input);
    }

    public Task WriteWarningAsync(string message)
    {
        _cmdlet.WriteWarning(message);
        return Task.CompletedTask;
    }

    public Task WriteErrorAsync(string message)
    {
        _cmdlet.WriteError(new ErrorRecord(
            new Exception(message),
            "CommandError",
            ErrorCategory.NotSpecified,
            null));
        return Task.CompletedTask;
    }

    public Task WriteColoredAsync(string message, OutputColor color)
    {
        var consoleColor = MapColor(color);
        _cmdlet.Host.UI.Write(consoleColor, _cmdlet.Host.UI.RawUI.BackgroundColor, message);
        return Task.CompletedTask;
    }

    private static ConsoleColor MapColor(OutputColor color) => color switch
    {
        OutputColor.Cyan => ConsoleColor.Cyan,
        OutputColor.Yellow => ConsoleColor.Yellow,
        OutputColor.Red => ConsoleColor.Red,
        OutputColor.Green => ConsoleColor.Green,
        _ => ConsoleColor.White
    };
}
