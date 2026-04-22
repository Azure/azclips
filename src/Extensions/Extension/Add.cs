using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Commands;

public class AddExtensionCommand : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = false,
    Description = @"The name of the extension to add.",
    SerializedName = @"Name")]
    public string? Name { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"The version of the extension to add.",
    SerializedName = @"Version")]
    public string? Version { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Feed URL to install the extension from.",
    SerializedName = @"FeedUrl")]
    public string? FeedUrl { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Path to the extension .nupkg file to add.",
    SerializedName = @"Path")]
    public string? Path { get; set; }

    public IProcessor Init(CommandContext context)
    {
        _writeOutput = context.CommandOutputHandler.WriteOutput;
        return this;
    }

    public async Task ProcessAsync()
    {
        var result = Process();
        await _writeOutput(result);
    }

    public object Process()
    {
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Path))
        {
            throw new ArgumentException("Either Name or Path must be provided to add an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(AddExtensionCommand)}.{nameof(Process)}");

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl);
        if (!string.IsNullOrWhiteSpace(Name))
        {
            var installPath = installer.InstallExtensionAsync(
                $"Microsoft.Azure.Cirrus.Extensions.{Name}",
                Version,
                includePrerelease: true,
                includeUnlisted: false,
                CancellationToken.None
            ).GetAwaiter().GetResult();
            return new { InstalledExtensionPath = installPath };
        }
        else
        {
            var installPath = installer.InstallFromPackageAsync(Path!, CancellationToken.None).GetAwaiter().GetResult();
            return new { InstalledExtensionPath = installPath };
        }
    }

    public string Process(bool Serialize)
    {
        var result = Process();
        if (Serialize)
        {
            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            return result.ToString() ?? "";
        }
    }
}