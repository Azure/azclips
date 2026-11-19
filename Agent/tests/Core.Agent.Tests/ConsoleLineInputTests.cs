using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract (#1295): a Windows console read is classified as CPython's
/// _PyOS_WindowsConsoleReadline does.
/// </summary>
public class ConsoleLineInputTests
{
    [Theory]
    [InlineData(true, 0, 995, '\0', "Interrupted")]
    [InlineData(true, 3, 0, '\x1a', "EndOfInput")]
    [InlineData(true, 0, 0, '\0', "EndOfInput")]
    [InlineData(true, 3, 0, 'y', "Line")]
    [InlineData(true, 3, 995, 'y', "Line")]
    [InlineData(false, 0, 6, '\0', "Error")]
    public void Classify_FollowsCPython(bool ok, int chars, int error, char first, string expected) =>
        Assert.Equal(expected, ConsoleLineInput.Classify(ok, chars, error, first).ToString());
}
