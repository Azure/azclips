using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

[Collection("Console")]
public class ConsoleAgentRendererTests
{
    [Fact]
    public void SpinnerFramesAndStop_WriteNothing_WhenStdoutIsRedirected()
    {
        var prevOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            Assert.True(Console.IsOutputRedirected);
            var renderer = new ConsoleAgentRenderer(quiet: true);

            renderer.OnSpinnerFrame("⠋", "Analyzing error root cause using AI");
            renderer.OnSpinnerFrame("⠙", "Analyzing error root cause using AI");
            renderer.OnSpinnerStop();

            Assert.Equal(string.Empty, writer.ToString());
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }
}
