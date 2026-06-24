using System.Text.Json;
using Core.Agent;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for the propose_plan content extractors introduced with the unified
/// display system (A20): <see cref="ChatAgent.ExtractIssues"/>,
/// <see cref="ChatAgent.ExtractUserInputNeeds"/> (placeholders), and
/// <see cref="ChatAgent.ExtractChosenDefaults"/>. They parse SDK-shaped JsonElement
/// args into the host's content-block views; absent/blank → null, valid → records.
/// </summary>
public class ProposePlanContentTests
{
    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    [Fact]
    public void Issues_parses_strings_and_skips_blanks()
    {
        var issues = ChatAgent.ExtractIssues(Args(new
        {
            issues = new[] { "No -g short flag", "   ", "--image not supported" },
        }));

        Assert.NotNull(issues);
        Assert.Equal(new[] { "No -g short flag", "--image not supported" }, issues);
    }

    [Fact]
    public void Issues_null_when_absent_or_all_blank()
    {
        Assert.Null(ChatAgent.ExtractIssues(Args(new { steps = System.Array.Empty<object>() })));
        Assert.Null(ChatAgent.ExtractIssues(Args(new { issues = new[] { "  ", "" } })));
    }

    [Fact]
    public void Placeholders_parses_name_and_description()
    {
        var ph = ChatAgent.ExtractUserInputNeeds(Args(new
        {
            placeholders = new[] { new { name = "admin-password", description = "Strong password" } },
        }), "placeholders");

        Assert.NotNull(ph);
        Assert.Single(ph!);
        Assert.Equal("admin-password", ph![0].Name);
        Assert.Equal("Strong password", ph[0].Description);
    }

    [Fact]
    public void Placeholders_skips_entries_with_blank_name()
    {
        var ph = ChatAgent.ExtractUserInputNeeds(Args(new
        {
            placeholders = new[]
            {
                new { name = "", description = "no name" },
                new { name = "ssh-key", description = "your key" },
            },
        }), "placeholders");

        Assert.NotNull(ph);
        Assert.Single(ph!);
        Assert.Equal("ssh-key", ph![0].Name);
    }

    [Fact]
    public void Defaults_parses_name_value_note()
    {
        var defs = ChatAgent.ExtractChosenDefaults(Args(new
        {
            defaults = new[]
            {
                new { name = "location", value = "eastus", note = "a common region" },
                new { name = "vm-size", value = "Standard_D2s_v3", note = "moderate size" },
            },
        }));

        Assert.NotNull(defs);
        Assert.Equal(2, defs!.Count);
        Assert.Equal("location", defs[0].Name);
        Assert.Equal("eastus", defs[0].Value);
        Assert.Equal("a common region", defs[0].Note);
    }

    [Fact]
    public void Defaults_null_when_absent()
    {
        Assert.Null(ChatAgent.ExtractChosenDefaults(Args(new { issues = new[] { "x" } })));
    }
}
