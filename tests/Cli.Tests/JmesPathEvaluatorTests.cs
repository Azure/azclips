// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Extensions;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.JmesPathEvaluator"/> covering extraction, projection,
/// no-match, and invalid-syntax handling.
/// </summary>
public class JmesPathEvaluatorTests
{
    [Fact]
    public void Transform_ShouldExtractField_ForSimpleIdentifier()
    {
        var result = Core.Cli.Output.JmesPathEvaluator.Transform("""{"name":"rg1","location":"eastus"}""", "name");

        Assert.Equal("\"rg1\"", result);
    }

    [Fact]
    public void Transform_ShouldProjectField_AcrossArray()
    {
        var result = Core.Cli.Output.JmesPathEvaluator.Transform(
            """[{"name":"rg1"},{"name":"rg2"}]""", "[].name");

        Assert.Equal("""["rg1","rg2"]""", result);
    }

    [Fact]
    public void Transform_ShouldSupportFilterExpressions()
    {
        var result = Core.Cli.Output.JmesPathEvaluator.Transform(
            """[{"name":"rg1","location":"eastus"},{"name":"rg2","location":"westus"}]""",
            "[?location=='westus'].name");

        Assert.Equal("""["rg2"]""", result);
    }

    [Fact]
    public void Transform_ShouldReturnNull_WhenNoMatch()
    {
        var result = Core.Cli.Output.JmesPathEvaluator.Transform("""{"name":"rg1"}""", "missing");

        Assert.Equal("null", result);
    }

    [Fact]
    public void Transform_ShouldReturnWholeDocument_ForCurrentNodeExpression()
    {
        var result = Core.Cli.Output.JmesPathEvaluator.Transform("""{"a":1}""", "@");

        Assert.Equal("""{"a":1}""", result);
    }

    [Fact]
    public void Transform_ShouldThrowUsageError_ForInvalidSyntax()
    {
        var ex = Assert.Throws<AzclipsErrorException>(
            () => Core.Cli.Output.JmesPathEvaluator.Transform("""{"a":1}""", "[[["));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("--query", ex.Message);
        Assert.Contains("[[[", ex.Message);
    }

    [Fact]
    public void Validate_ShouldAcceptSyntaxThatDependsOnResultType()
    {
        Core.Cli.Output.JmesPathEvaluator.Validate("length(@)");
    }

    [Fact]
    public void FormatValue_ShouldApplyQueryBeforeRendering()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            new[] { new { name = "rg1" }, new { name = "rg2" } },
            OutputFormat.Json,
            colorPolicyAllowsColor: false,
            query: "[].name");

        Assert.Equal("[\"rg1\",\"rg2\"]", result);
    }
}
