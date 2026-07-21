// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Tui;
using Core.Tui.Services;

namespace Cli.Tests;

/// <summary>
/// Unit tests for the TUI's stderr-line classifier. The classifier maps CLI
/// diagnostic prefixes onto <see cref="HistoryItemType"/> values so warnings
/// don't render as errors under <c>ProcessService.RunCliAsync</c>.
/// </summary>
public class TuiClassifyLineTests
{
    [Fact]
    public void ClassifyLine_ErrorPrefix_MapsToError()
    {
        var result = ProcessService.ClassifyLine("ERROR: something bad", HistoryItemType.Error);

        Assert.NotNull(result);
        Assert.Equal(HistoryItemType.Error, result.Value.type);
        Assert.Equal("ERROR: something bad", result.Value.text);
    }

    [Theory]
    [InlineData("WARNING: something noteworthy")]
    [InlineData("INFO: something informational")]
    public void ClassifyLine_WarningOrInfoPrefix_MapsToInfo(string line)
    {
        // Default under RunCliAsync is Error — this verifies warnings/info escape that stamping.
        var result = ProcessService.ClassifyLine(line, HistoryItemType.Error);

        Assert.NotNull(result);
        Assert.Equal(HistoryItemType.Info, result.Value.type);
    }

    [Theory]
    [InlineData("VERBOSE: chatty log")]
    [InlineData("DEBUG: deep diagnostic")]
    public void ClassifyLine_VerboseOrDebugPrefix_IsDropped(string line)
    {
        var result = ProcessService.ClassifyLine(line, HistoryItemType.Error);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("warning: git-style lowercase")]
    [InlineData("error: rustc-style lowercase")]
    [InlineData("info: npm-style lowercase")]
    public void ClassifyLine_LowercasePrefixes_FallThroughToDefault(string line)
    {
        // git / rustc / npm emit lowercase prefixes. The AzClips classifier is case-sensitive
        // so those pass through as HistoryItemType.Output under RunLocalAsync, preserving
        // pre-existing behavior for third-party tools.
        var result = ProcessService.ClassifyLine(line, HistoryItemType.Output);

        Assert.NotNull(result);
        Assert.Equal(HistoryItemType.Output, result.Value.type);
    }

    [Fact]
    public void ClassifyLine_UnprefixedLine_UsesCallerDefault()
    {
        var errResult = ProcessService.ClassifyLine("bare stderr text", HistoryItemType.Error);
        Assert.NotNull(errResult);
        Assert.Equal(HistoryItemType.Error, errResult.Value.type);

        var outResult = ProcessService.ClassifyLine("bare stdout text", HistoryItemType.Output);
        Assert.NotNull(outResult);
        Assert.Equal(HistoryItemType.Output, outResult.Value.type);
    }

    [Fact]
    public void ClassifyLine_StripsAnsiEscapesBeforeClassifying()
    {
        // AnsiRed + "ERROR: red text" + AnsiReset. Should still classify as Error.
        var line = "\x1b[31mERROR: red text\x1b[0m";

        var result = ProcessService.ClassifyLine(line, HistoryItemType.Output);

        Assert.NotNull(result);
        Assert.Equal(HistoryItemType.Error, result.Value.type);
        Assert.Equal("ERROR: red text", result.Value.text);
    }

    [Fact]
    public void ClassifyLine_TrimsLeadingWhitespaceBeforeMatching()
    {
        // Indented AzClips diagnostic — still classified by prefix after trim.
        var result = ProcessService.ClassifyLine("   ERROR: indented", HistoryItemType.Output);

        Assert.NotNull(result);
        Assert.Equal(HistoryItemType.Error, result.Value.type);
    }
}
