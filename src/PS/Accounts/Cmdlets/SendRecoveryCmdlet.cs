// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Agent.Providers;
using Core.DependencyInjection;
using Core.Telemetry;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>
/// Sends a selected AzCLIPS PowerShell error, or the latest one, to Azure AI for recovery advice.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "AzRecovery", SupportsShouldProcess = true)]
[OutputType(typeof(string))]
public class SendRecoveryCmdlet : PSCmdlet
{
    private static readonly HttpClient s_http = new(new HttpClientHandler { AllowAutoRedirect = false });
    private readonly CancellationTokenSource _cts = new();

    [Parameter]
    public string? Id { get; set; }

    [Parameter]
    [ValidateSet("oneshot", "interactive", IgnoreCase = true)]
    public string? Style { get; set; }

    protected override void ProcessRecord()
    {
        if (Id is not null && !FailureId.TryParse(Id, out _))
            throw new PSArgumentException("Invalid recovery ID. Use the format <process-id>-<8 lowercase hex digits>, such as 48212-cfd9ddb4.", nameof(Id));

        var options = AzclipsServiceProvider.GetRequiredService<IConfigurationService>().GetConfiguration<AgentOptions>();
        if (!options.Recovery.IsEnabled)
            throw new PSInvalidOperationException(
                "AI recovery is off. Set agent.recovery.trigger to manual or auto before troubleshooting. " +
                "In PowerShell, run: Set-AzConfig -Key agent.recovery.trigger -Value manual");
        if (!options.IsBuiltinProvider)
            throw new PSInvalidOperationException("PowerShell recovery requires agent.provider to be builtin.");

        var failure = PowerShellRecoveryHistory.Read(SessionState).FirstOrDefault(error => Id is null || Id == error.Id)
            ?? throw new PSInvalidOperationException(Id is null
                ? "No Azure PowerShell errors are available in the current session."
                : "That recovery ID is not in this session. Use Get-AzRecoveryId to list available errors.");

        var style = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, Style);
        if (!ShouldProcess(failure.Id, "Send Azure PowerShell error to Azure AI"))
            return;

        var endpointWarnings = new List<string>();
        var (endpoint, accessToken) = ResolveModelAsync(options, endpointWarnings.Add, _cts.Token).GetAwaiter().GetResult();
        foreach (var warning in endpointWarnings)
            WriteWarning(warning);

        List<RecoveryMessage> messages =
        [
            new("user", $"Command: {failure.Error.InvocationInfo.MyCommand.Name}\nError: {failure.Error}"),
        ];
        while (true)
        {
            var answer = SendAsync(s_http, endpoint, options.Builtin, accessToken, messages, style, _cts.Token).GetAwaiter().GetResult();
            WriteObject(answer);
            if (!style.IsInteractive)
                return;

            Host.UI.Write("Follow-up (Enter to finish): ");
            var followUp = Host.UI.ReadLine();
            if (string.IsNullOrWhiteSpace(followUp))
                return;
            messages.Add(new("assistant", answer));
            messages.Add(new("user", followUp));
        }
    }

    private static async Task<(string Endpoint, string AccessToken)> ResolveModelAsync(
        AgentOptions options, Action<string> reportWarning, CancellationToken token)
    {
        var profile = await AzclipsServiceProvider.GetRequiredService<IProfileService>()
            .GetActiveProfileAsync(token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sign in with Connect-AzAccount before using AI recovery.");
        var auth = await AzclipsServiceProvider.GetRequiredService<IAuthService>()
            .AcquireTokenSilentlyAsync([BuiltinProviderOptions.ArmScope], profile.TenantId,
                cancellationToken: token, profile: profile).ConfigureAwait(false);
        if (string.IsNullOrEmpty(auth.AccessToken))
            throw new InvalidOperationException("Sign in with Connect-AzAccount before using AI recovery.");

        var boundary = await AzclipsServiceProvider.GetRequiredService<DataBoundaryResolver>()
            .ResolveAndCacheAsync(profile.TenantId, _ => Task.FromResult<string?>(auth.AccessToken),
                TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
        return (BuiltinEndpointResolver.Choose(boundary, options.Builtin, reportWarning).Url, auth.AccessToken);
    }

    internal sealed record RecoveryMessage(string Role, string Content);

    internal static async Task<string> SendAsync(HttpClient http, string endpoint, BuiltinProviderOptions options,
        string accessToken, IReadOnlyList<RecoveryMessage> messages, RecoverySessionSettings style, CancellationToken token)
    {
        var promptName = $"RecoveryPrompts.{style.SessionStyle}.md";
        using var prompt = new StreamReader(typeof(SendRecoveryCmdlet).Assembly.GetManifestResourceStream(promptName)
            ?? throw new InvalidOperationException($"The embedded PowerShell recovery prompt '{promptName}' was not found."));
        var url = $"{endpoint.TrimEnd('/')}/responses?api-version={Uri.EscapeDataString(options.ApiVersion)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            model = options.ModelId,
            instructions = await prompt.ReadToEndAsync(token).ConfigureAwait(false),
            input = messages,
            store = false,
        });
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var content = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Azure AI returned an empty recovery response.");

        var text = from item in content.RootElement.GetProperty("output").EnumerateArray()
                   where item.GetProperty("type").GetString() == "message"
                   from part in item.GetProperty("content").EnumerateArray()
                   where part.GetProperty("type").GetString() == "output_text"
                   select part.GetProperty("text").GetString();
        var answer = string.Join(Environment.NewLine, text);
        return !string.IsNullOrWhiteSpace(answer) ? answer
            : throw new InvalidOperationException("Azure AI returned no recovery advice.");
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
