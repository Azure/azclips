using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="ChatAgent.ExtractCommandName"/> — pulling the command
/// verb out of a failed invocation so the host can pre-fetch its help. The key
/// behavior is conservatism: real command invocations yield a verb; natural
/// language / odd input yields null (no pre-fetch, agent falls back to its tools).
/// </summary>
public class ExtractCommandNameTests
{
    [Fact]
    public void Extracts_two_word_verb_up_to_first_flag()
    {
        // The actual e2e trigger.
        var name = ChatAgent.ExtractCommandName("vm create -g rg-x -n myvm --image Ubuntu2204");
        Assert.Equal("vm create", name);
    }

    [Fact]
    public void Extracts_single_word_verb()
    {
        Assert.Equal("login", ChatAgent.ExtractCommandName("login --use-device-code"));
    }

    [Fact]
    public void Keeps_hyphenated_verb_token()
    {
        Assert.Equal("vm create-combined", ChatAgent.ExtractCommandName("vm create-combined --name x"));
    }

    [Fact]
    public void Returns_misspelled_command_verbatim_for_lookup_to_judge()
    {
        // A typo'd command is still a command shape — pass it through so LookupCommand
        // returns not_found + suggestions (which steers the model to the right name).
        Assert.Equal("vm createt", ChatAgent.ExtractCommandName("vm createt -g rg-x"));
    }

    [Fact]
    public void Returns_null_for_natural_language()
    {
        // A CJK / free-text trigger is NOT a command — don't pre-fetch garbage.
        Assert.Null(ChatAgent.ExtractCommandName("帮我创建一个虚拟机"));
        Assert.Null(ChatAgent.ExtractCommandName("create me a VM please"));
    }

    [Fact]
    public void Returns_null_when_starting_with_a_flag()
    {
        Assert.Null(ChatAgent.ExtractCommandName("--help"));
    }

    [Fact]
    public void Returns_null_for_empty_or_whitespace()
    {
        Assert.Null(ChatAgent.ExtractCommandName(""));
        Assert.Null(ChatAgent.ExtractCommandName("   "));
        Assert.Null(ChatAgent.ExtractCommandName(null));
    }

    [Fact]
    public void Caps_verb_length_so_it_does_not_eat_positional_args()
    {
        // Four bare words max; the rest are treated as args, not part of the verb.
        var name = ChatAgent.ExtractCommandName("a b c d e f g");
        Assert.Equal("a b c d", name);
    }
}
