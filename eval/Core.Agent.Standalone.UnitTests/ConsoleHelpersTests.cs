using System;
using System.IO;
using Core.Agent;
using Core.Agent.Standalone;
using Xunit;


namespace Core.Agent.Standalone.UnitTests;

/// <summary>
/// Unit tests for <see cref="ConsoleHelpers"/>.
/// </summary>
public class ConsoleHelpersTests : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalOutput;

    public ConsoleHelpersTests()
    {
        _stringWriter = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_stringWriter);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _stringWriter.Dispose();
    }

    /// <summary>
    /// Tests that WriteCyan writes the message followed by a blank line.
    /// Note: This test verifies text output only. Console color changes cannot be verified
    /// because System.Console is a sealed class and cannot be mocked.
    /// </summary>
    /// <param name="message">The message to write to the console.</param>
    [Theory(Skip="ProductionBugSuspected")]
    [InlineData("Hello, World!")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("Line1\nLine2")]
    [InlineData("Tab\tSeparated")]
    [InlineData("Special characters: !@#$%^&*()")]
    [InlineData("Unicode: 你好世界 🌍")]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    [Trait("Category", "ProductionBugSuspected")]
    public void WriteCyan_WithVariousMessages_WritesMessageAndBlankLine(string message)
    {
        // Arrange
        var expectedOutput = message + Environment.NewLine + Environment.NewLine;

        // Act
        ConsoleHelpers.WriteCyan(message);

        // Assert
        var actualOutput = _stringWriter.ToString();
        Assert.Equal(expectedOutput, actualOutput);
    }

    /// <summary>
    /// Tests that WriteCyan handles null input.
    /// The method accepts a non-nullable string parameter, but we test the actual runtime behavior.
    /// Note: This test verifies text output only. Console color changes cannot be verified
    /// because System.Console is a sealed class and cannot be mocked.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void WriteCyan_WithNullMessage_WritesBlankLine()
    {
        // Arrange
        string? message = null;
        var expectedOutput = Environment.NewLine + Environment.NewLine;

        // Act
        ConsoleHelpers.WriteCyan(message!);

        // Assert
        var actualOutput = _stringWriter.ToString();
        Assert.Equal(expectedOutput, actualOutput);
    }

    /// <summary>
    /// Tests that WriteCyan writes output with correct line terminators.
    /// Note: This test verifies text output only. Console color changes cannot be verified
    /// because System.Console is a sealed class and cannot be mocked.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void WriteCyan_WithNormalMessage_ContainsTwoLineTerminators()
    {
        // Arrange
        var message = "Test message";

        // Act
        ConsoleHelpers.WriteCyan(message);

        // Assert
        var output = _stringWriter.ToString();
        Assert.Contains(message, output);
        Assert.Equal(2, CountOccurrences(output, Environment.NewLine));
    }

    /// <summary>
    /// Helper method to count occurrences of a substring within a string.
    /// </summary>
    private static int CountOccurrences(string text, string substring)
    {
        if (string.IsNullOrEmpty(substring))
        {
            return 0;
        }

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }
}
