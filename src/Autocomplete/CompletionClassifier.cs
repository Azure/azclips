namespace Cirrus.Autocomplete;

/// <summary>
/// Classifies an in-progress command line into a completion type that the
/// autocomplete agent can use as a hint.
///
/// Pulled out of Program.cs so it is independently testable.
/// </summary>
internal static class CompletionClassifier
{
    public const string Command = "command";
    public const string Subcommand = "subcommand";
    public const string Option = "option";
    public const string Value = "value";
    public const string Unknown = "unknown";

    public static string Classify(IReadOnlyList<string> tokens, string prefix, string line)
    {
        if (tokens.Count <= 1)
        {
            return Command;
        }

        string lastToken = tokens[^1];
        bool endsInSpace = line.EndsWith(' ');

        if (lastToken.StartsWith("--", StringComparison.Ordinal) && endsInSpace)
        {
            return Value;
        }

        if (prefix.StartsWith("--", StringComparison.Ordinal) || prefix.StartsWith('-'))
        {
            return Option;
        }

        if (tokens.Count == 2)
        {
            return Subcommand;
        }

        return Unknown;
    }
}
