using Cirrus.Autocomplete;

namespace Autocomplete.Tests;

public class CompletionClassifierTests
{
    [Fact]
    public void Classify_ShouldReturnCommand_WhenOnlyAzv2Token()
    {
        string[] tokens = { "azv2" };

        string result = CompletionClassifier.Classify(tokens, prefix: string.Empty, line: "azv2 ");

        Assert.Equal(CompletionClassifier.Command, result);
    }

    [Fact]
    public void Classify_ShouldReturnSubcommand_WhenSecondTokenInProgress()
    {
        string[] tokens = { "azv2", "gr" };

        string result = CompletionClassifier.Classify(tokens, prefix: "gr", line: "azv2 gr");

        Assert.Equal(CompletionClassifier.Subcommand, result);
    }

    [Fact]
    public void Classify_ShouldReturnSubcommand_WhenSecondTokenComplete()
    {
        string[] tokens = { "azv2", "group" };

        string result = CompletionClassifier.Classify(tokens, prefix: "group", line: "azv2 group");

        Assert.Equal(CompletionClassifier.Subcommand, result);
    }

    [Fact]
    public void Classify_ShouldReturnOption_WhenPrefixStartsWithDoubleDash()
    {
        string[] tokens = { "azv2", "group", "create", "--" };

        string result = CompletionClassifier.Classify(tokens, prefix: "--", line: "azv2 group create --");

        Assert.Equal(CompletionClassifier.Option, result);
    }

    [Fact]
    public void Classify_ShouldReturnOption_WhenPrefixStartsWithSingleDash()
    {
        string[] tokens = { "azv2", "group", "create", "-l" };

        string result = CompletionClassifier.Classify(tokens, prefix: "-l", line: "azv2 group create -l");

        Assert.Equal(CompletionClassifier.Option, result);
    }

    [Fact]
    public void Classify_ShouldReturnValue_WhenLastTokenIsOptionAndLineEndsInSpace()
    {
        string[] tokens = { "azv2", "group", "create", "--location" };

        string result = CompletionClassifier.Classify(tokens, prefix: string.Empty, line: "azv2 group create --location ");

        Assert.Equal(CompletionClassifier.Value, result);
    }

    [Fact]
    public void Classify_ShouldReturnUnknown_WhenThreePositionalTokens()
    {
        string[] tokens = { "azv2", "group", "create" };

        string result = CompletionClassifier.Classify(tokens, prefix: "create", line: "azv2 group create");

        Assert.Equal(CompletionClassifier.Unknown, result);
    }

    [Fact]
    public void Classify_ShouldReturnCommand_WhenTokensEmpty()
    {
        string[] tokens = Array.Empty<string>();

        string result = CompletionClassifier.Classify(tokens, prefix: string.Empty, line: string.Empty);

        Assert.Equal(CompletionClassifier.Command, result);
    }
}
