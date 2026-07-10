using Core.Agent;
using Core.Agent.Plan;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="FailedCommandHelp.ExtractCommandName"/> — pulling the command
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
        var name = FailedCommandHelp.ExtractCommandName("vm create -g rg-x -n myvm --image Ubuntu2204");
        Assert.Equal("vm create", name);
    }

    [Fact]
    public void Extracts_single_word_verb()
    {
        Assert.Equal("login", FailedCommandHelp.ExtractCommandName("login --use-device-code"));
    }

    [Fact]
    public void Keeps_hyphenated_verb_token()
    {
        Assert.Equal("vm create-basic", FailedCommandHelp.ExtractCommandName("vm create-basic --name x"));
    }

    [Fact]
    public void Returns_misspelled_command_verbatim_for_lookup_to_judge()
    {
        // A typo'd command is still a command shape — pass it through so LookupCommand
        // returns not_found + suggestions (which steers the model to the right name).
        Assert.Equal("vm createt", FailedCommandHelp.ExtractCommandName("vm createt -g rg-x"));
    }

    [Fact]
    public void Returns_null_for_natural_language()
    {
        // A CJK / free-text trigger is NOT a command — don't pre-fetch garbage.
        Assert.Null(FailedCommandHelp.ExtractCommandName("帮我创建一个虚拟机"));
        Assert.Null(FailedCommandHelp.ExtractCommandName("create me a VM please"));
    }

    [Fact]
    public void Returns_null_when_starting_with_a_flag()
    {
        Assert.Null(FailedCommandHelp.ExtractCommandName("--help"));
    }

    [Fact]
    public void Returns_null_for_empty_or_whitespace()
    {
        Assert.Null(FailedCommandHelp.ExtractCommandName(""));
        Assert.Null(FailedCommandHelp.ExtractCommandName("   "));
        Assert.Null(FailedCommandHelp.ExtractCommandName(null));
    }

    [Fact]
    public void Caps_verb_length_so_it_does_not_eat_positional_args()
    {
        // Four bare words max; the rest are treated as args, not part of the verb.
        var name = FailedCommandHelp.ExtractCommandName("a b c d e f g");
        Assert.Equal("a b c d", name);
    }
}
