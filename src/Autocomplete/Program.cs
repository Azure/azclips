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

            AutocompleteAgent? agent = AutocompleteAgent.TryCreate();
            if (agent == null)
            {
                return 1;
            }

            using var cts = new CancellationTokenSource(AgentTimeout);

            List<SuggestionItem> items;
            try
            {
                items = await agent.GetSuggestionsAsync(request, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
            catch (Exception)
            {
                return 1;
            }

            if (items.Count == 0)
            {
                return 1;
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
                return 1;
            }

            Console.Write(selected);
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }

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

        string completionType = ClassifyCompletion(tokens, prefix, line);

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

    private static string ClassifyCompletion(List<string> tokens, string prefix, string line)
    {
        if (tokens.Count <= 1)
        {
            return "command";
        }

        string lastToken = tokens[^1];
        bool endsInSpace = line.EndsWith(' ');

        if (lastToken.StartsWith("--", StringComparison.Ordinal) && endsInSpace)
        {
            return "value";
        }

        if (prefix.StartsWith("--", StringComparison.Ordinal) || prefix.StartsWith('-'))
        {
            return "option";
        }

        if (tokens.Count == 2)
        {
            return "subcommand";
        }

        return "unknown";
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
        return 1500;
    }
}
