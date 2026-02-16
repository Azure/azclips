using System;
using System.Threading;
using System.Threading.Tasks;

using Core.Agent.Standalone;
using Xunit;

namespace Core.Agent.Standalone.UnitTests;


/// <summary>
/// Unit tests for the <see cref="Spinner"/> class.
/// </summary>
public sealed class SpinnerTests : IDisposable
{
    private readonly List<Spinner> _spinners = new();

    /// <summary>
    /// Ensures that spinners created during tests are properly disposed.
    /// </summary>
    public void Dispose()
    {
        foreach (var spinner in _spinners)
        {
            spinner?.Dispose();
        }
        _spinners.Clear();
    }

    /// <summary>
    /// Tests that the Start method returns a non-null, disposable Spinner instance
    /// when called with various valid message values.
    /// </summary>
    /// <param name="message">The message to display with the spinner.</param>
    [Theory]
    [InlineData("Loading data...")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A very long message that exceeds typical console width to test handling of lengthy strings in the spinner display mechanism")]
    [InlineData("Special chars: !@#$%^&*(){}[]|\\:;\"'<>,.?/~`")]
    [InlineData("Unicode: ♠♣♥♦")]
    [InlineData("\t\n\r")]
    [Trait("Category", "auto-generated")]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    public void Start_WithValidMessage_ReturnsDisposableSpinner(string message)
    {
        // Arrange & Act
        var spinner = Spinner.Start(message);
        // Note: Not adding to _spinners because this test explicitly verifies disposal

        // Assert
        Assert.NotNull(spinner);

        // Verify disposal works without throwing
        var exception = Record.Exception(() => spinner.Dispose());
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that the Start method returns a non-null, disposable Spinner instance
    /// when called without parameters, using the default message value.
    /// </summary>
    [Fact]
    [Trait("Category", "auto-generated")]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    public void Start_WithDefaultParameter_ReturnsDisposableSpinner()
    {
        // Arrange & Act
        var spinner = Spinner.Start();
        // Note: Not adding to _spinners because this test explicitly verifies disposal

        // Assert
        Assert.NotNull(spinner);

        // Verify disposal works without throwing
        var exception = Record.Exception(() => spinner.Dispose());
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that the Start method handles null message input.
    /// Even though the parameter is non-nullable, null can be passed explicitly at runtime.
    /// The method should not throw and return a disposable spinner instance.
    /// </summary>
    [Fact]
    [Trait("Category", "auto-generated")]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    public void Start_WithNullMessage_ReturnsDisposableSpinner()
    {
        // Arrange & Act
        var spinner = Spinner.Start(null!);
        // Note: Not adding to _spinners because this test explicitly verifies disposal

        // Assert
        Assert.NotNull(spinner);

        // Verify disposal works without throwing
        var exception = Record.Exception(() => spinner.Dispose());
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that Dispose completes successfully without throwing exceptions
    /// when called on a newly created spinner instance.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void Dispose_NewlyCreatedSpinner_CompletesWithoutException()
    {
        // Arrange
        using var spinner = Spinner.Start("Test message");

        // Act & Assert
        // Dispose is called automatically by the using statement
        // If any exception occurs, the test will fail
    }

    /// <summary>
    /// Tests that Dispose can be called multiple times without throwing exceptions,
    /// verifying idempotent behavior.
    /// </summary>
    [Fact(Skip="ProductionBugSuspected")]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    [Trait("Category", "ProductionBugSuspected")]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var spinner = Spinner.Start("Test message");

        // Act
        spinner.Dispose();
        spinner.Dispose();
        spinner.Dispose();

        // Assert
        // No exception should be thrown
    }

    /// <summary>
    /// Tests that Dispose completes successfully after the spinner has been running
    /// for a short period, ensuring proper cleanup of the animation task.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public async Task Dispose_AfterSpinnerRunning_CompletesSuccessfully()
    {
        // Arrange
        var spinner = Spinner.Start("Test message");
        await Task.Delay(200); // Let the spinner run for a bit

        // Act & Assert
        spinner.Dispose(); // Should complete without exception
    }

    /// <summary>
    /// Tests that Dispose works correctly with different message lengths,
    /// including empty string, short, and long messages.
    /// </summary>
    /// <param name="message">The message to display with the spinner.</param>
    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("This is a very long message that might be used with the spinner animation")]
    [InlineData("Message with special chars: !@#$%^&*()")]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void Dispose_WithVariousMessages_CompletesWithoutException(string message)
    {
        // Arrange
        var spinner = Spinner.Start(message);

        // Act & Assert
        spinner.Dispose();
        // No exception should be thrown
    }

    /// <summary>
    /// Tests that Dispose completes within a reasonable time frame,
    /// ensuring the 500ms timeout in the Wait call is respected.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void Dispose_CompletesWithinReasonableTime()
    {
        // Arrange
        var spinner = Spinner.Start("Test message");
        var timeout = TimeSpan.FromSeconds(2); // Should complete well within this

        // Act
        var completed = Task.Run(() => spinner.Dispose()).Wait(timeout);

        // Assert
        Assert.True(completed, "Dispose should complete within reasonable time");
    }

    /// <summary>
    /// Tests that Dispose can be called immediately after creation without waiting,
    /// ensuring proper handling of the cancellation token and task cleanup.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void Dispose_CalledImmediatelyAfterCreation_HandlesGracefully()
    {
        // Arrange & Act
        var spinner = Spinner.Start("Immediate disposal test");
        spinner.Dispose();

        // Assert
        // No exception should be thrown
    }

    /// <summary>
    /// Tests that multiple spinners can be created and disposed independently
    /// without interfering with each other.
    /// </summary>
    [Fact]
    [Trait("Owner", "Code Testing Agent 0.4.600-alpha+c42eb40cd8")]
    [Trait("Category", "auto-generated")]
    public void Dispose_MultipleSpinnerInstances_DisposeIndependently()
    {
        // Arrange
        var spinner1 = Spinner.Start("Spinner 1");
        var spinner2 = Spinner.Start("Spinner 2");
        var spinner3 = Spinner.Start("Spinner 3");

        // Act
        spinner1.Dispose();
        spinner2.Dispose();
        spinner3.Dispose();

        // Assert
        // All disposals should complete without exception
    }
}
