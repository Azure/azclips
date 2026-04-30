using Cirrus.Autocomplete.Agent;
using Cirrus.Autocomplete.MenuHost;

namespace Cirrus.Autocomplete;

/// <summary>
/// Entry point for the dedicated azv2 autocomplete process.
///
/// Iteration 1 contract:
/// - Invoked by the Bash completion hook on Tab.
/// - Reads --line, --cursor, --prefix from args.
/// - Calls a remote LLM via AutocompleteAgent (gpt-5x-nano).
/// - Renders an interactive menu.
/// - Writes the selected token to stdout.
/// - Never executes commands. Never starts azv2.
/// </summary>
internal static class Program
{
    private static readonly TimeSpan AgentTimeout = TimeSpan.FromMilliseconds(GetTimeoutMs());

    private static async Task<int> Main(string[] args)
    {
        try
        {
            CompletionRequest request = ParseArgs(args);

            AutocompleteAgent agent = await AutocompleteAgent.CreateAsync().ConfigureAwait(false);

            using var cts = new CancellationTokenSource(AgentTimeout);

            string userPayload = System.Text.Json.JsonSerializer.Serialize(request);

            // Show a spinner while waiting for the LLM. Written to /dev/tty so it
            // appears to the user even though stdout is captured by bash COMPREPLY.
            string responseText;
            await using (Spinner.Start())
            {
                responseText = await agent.RunAsync(userPayload, cts.Token).ConfigureAwait(false);
            }

            List<SuggestionItem> items = SuggestionParser.Parse(responseText, request.Prefix);

            if (items.Count == 0)
            {
                Console.Error.WriteLine("azv2-autocomplete: no suggestions returned by model.");
                Console.Error.WriteLine($"  raw response: {Truncate(responseText, 500)}");
                return 2;
            }

            int max = GetMaxSuggestions();
            if (items.Count > max)
            {
                items = items.GetRange(0, max);
            }

            var displayLines = items.Select(item => item.Display ?? item.Value).ToList();
            var values = items.Select(item => item.Value).ToList();

            var menu = new MenuRenderer(displayLines, values);
            string? selected = menu.Run();

            if (string.IsNullOrEmpty(selected))
            {
                // User cancelled the menu — not an error.
                return 3;
            }

            Console.Write(selected);
            return 0;
        }
        catch (OperationCanceledException ex)
        {
            Console.Error.WriteLine($"azv2-autocomplete: timed out after {AgentTimeout.TotalMilliseconds:F0} ms.");
            Console.Error.WriteLine($"  {ex.Message}");
            return 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"azv2-autocomplete: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"  inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "...");


    private static CompletionRequest ParseArgs(string[] args)
    {
        string line = string.Empty;
        int cursor = 0;
        string prefix = string.Empty;

        for (int i = 0; i < args.Length; i++)
        {
            string current = args[i];
            if (current == "--line" && i + 1 < args.Length)
            {
                line = args[++i];
            }
            else if (current == "--cursor" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out cursor);
            }
            else if (current == "--prefix" && i + 1 < args.Length)
            {
                prefix = args[++i];
            }
        }

        if (string.IsNullOrEmpty(line) && !string.IsNullOrEmpty(prefix))
        {
            line = "azv2 " + prefix;
            cursor = line.Length;
        }
        else if (string.IsNullOrEmpty(line))
        {
            line = "azv2";
            cursor = line.Length;
        }

        var tokens = line
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        string completionType = CompletionClassifier.Classify(tokens, prefix, line);

        return new CompletionRequest
        {
            Shell = "bash",
            Line = line,
            Cursor = cursor,
            Prefix = prefix,
            Tokens = tokens,
            CompletionType = completionType,
        };
    }

    private static int GetMaxSuggestions()
    {
        string? raw = Environment.GetEnvironmentVariable("AZV2_AUTOCOMPLETE_MAX_SUGGESTIONS");
        if (int.TryParse(raw, out int value) && value > 0)
        {
            return value;
        }
        return 12;
    }

    private static int GetTimeoutMs()
    {
        string? raw = Environment.GetEnvironmentVariable("AZV2_AUTOCOMPLETE_TIMEOUT_MS");
        if (int.TryParse(raw, out int value) && value > 0)
        {
            return value;
        }
        return 10000;
    }
}
