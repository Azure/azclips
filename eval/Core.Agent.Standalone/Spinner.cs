namespace Core.Agent.Standalone;

/// <summary>
/// Displays an animated spinner in the console while work is being performed.
/// Implements IDisposable to automatically stop and clean up the spinner.
/// </summary>
internal sealed class Spinner : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _task;
    private readonly string _message;

    private static readonly string[] SpinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    private Spinner(string message)
    {
        _message = message;
        _cts = new CancellationTokenSource();
        _task = Task.Run(AnimateAsync, _cts.Token);
    }

    /// <summary>
    /// Creates and starts a new spinner with the specified message.
    /// </summary>
    public static Spinner Start(string message = "Thinking...")
    {
        return new Spinner(message);
    }

    private async Task AnimateAsync()
    {
        int frameIndex = 0;
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                Console.Write($"\r{SpinnerFrames[frameIndex++ % SpinnerFrames.Length]} {_message}");
                await Task.Delay(100, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        finally
        {
            // Clear the spinner line
            Console.Write("\r" + new string(' ', _message.Length + 3) + "\r");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _task.Wait(500);
        }
        catch
        {
            // Ignore exceptions during cleanup
        }
        _cts.Dispose();
    }
}
