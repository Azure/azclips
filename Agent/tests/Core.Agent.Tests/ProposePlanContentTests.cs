using System.Text.Json;
using Core.Agent;
using Core.Agent.Plan;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for the propose_plan content extractors introduced with the unified
/// display system (A20): <see cref="PlanArgsParser.ExtractIssues"/>,
/// <see cref="PlanArgsParser.ExtractUserInputNeeds"/> (placeholders), and
/// <see cref="PlanArgsParser.ExtractChosenDefaults"/>. They parse SDK-shaped JsonElement
/// args into the host's content-block views; absent/blank → null, valid → records.
/// </summary>
public class ProposePlanContentTests
{
    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    [Fact]
    public void Issues_parses_strings_and_skips_blanks()
    {
        var issues = PlanArgsParser.ExtractIssues(Args(new
        {
            issues = new[] { "No -g short flag", "   ", "--image not supported" },
        }));

        Assert.NotNull(issues);
        Assert.Equal(new[] { "No -g short flag", "--image not supported" }, issues);
    }

    [Fact]
    public void Issues_null_when_absent_or_all_blank()
    {
        Assert.Null(PlanArgsParser.ExtractIssues(Args(new { steps = System.Array.Empty<object>() })));
        Assert.Null(PlanArgsParser.ExtractIssues(Args(new { issues = new[] { "  ", "" } })));
    }

    [Fact]
    public void Placeholders_parses_flag_and_description()
    {
        var ph = PlanArgsParser.ExtractUserInputNeeds(Args(new
        {
            placeholders = new[] { new { flag = "--admin-password", description = "Strong password" } },
        }), "placeholders");

        Assert.NotNull(ph);
        Assert.Single(ph!);
        Assert.Equal("--admin-password", ph![0].Flag);
        Assert.Equal("Strong password", ph[0].Description);
    }

    [Fact]
    public void Placeholders_skips_entries_with_blank_flag()
    {
        var ph = PlanArgsParser.ExtractUserInputNeeds(Args(new
        {
            placeholders = new[]
            {
                new { flag = "", description = "no flag" },
                new { flag = "--ssh-key", description = "your key" },
            },
        }), "placeholders");

        Assert.NotNull(ph);
        Assert.Single(ph!);
        Assert.Equal("--ssh-key", ph![0].Flag);
    }

    [Fact]
    public void Defaults_parses_flag_note()
    {
        var defs = PlanArgsParser.ExtractChosenDefaults(Args(new
        {
            defaults = new[]
            {
                new { flag = "--location", note = "a common region" },
                new { flag = "--size", note = "moderate size" },
            },
        }));

        Assert.NotNull(defs);
        Assert.Equal(2, defs!.Count);
        Assert.Equal("--location", defs[0].Flag);
        Assert.Equal("a common region", defs[0].Note);
    }

    [Fact]
    public void Defaults_tolerates_legacy_name_key()
    {
        // A model still emitting the old {name,...} shape should degrade to the
        // flag name, not drop the entry.
        var defs = PlanArgsParser.ExtractChosenDefaults(Args(new
        {
            defaults = new[] { new { name = "location", note = "a common region" } },
        }));

        Assert.NotNull(defs);
        Assert.Equal("location", defs![0].Flag);
    }

    [Fact]
    public void Defaults_null_when_absent()
    {
        Assert.Null(PlanArgsParser.ExtractChosenDefaults(Args(new { issues = new[] { "x" } })));
    }
}
