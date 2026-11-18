// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Language;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Agent.Providers;
using Core.DependencyInjection;
using Core.Telemetry;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ParameterMetadata = System.Management.Automation.ParameterMetadata;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>
/// Sends a selected AzCLIPS PowerShell error, or the latest one, to Azure AI for recovery advice.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "AzRecovery", SupportsShouldProcess = true)]
[OutputType(typeof(string))]
public class SendRecoveryCmdlet : PSCmdlet
{
    private static readonly HttpClient s_http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromMinutes(10),
    };
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
                "Install and import the AzCLIPS Az.Config module, then run: Set-AzConfig -Key agent.recovery.trigger -Value manual");
        if (!options.IsBuiltinProvider)
            throw new PSInvalidOperationException("PowerShell recovery requires agent.provider to be builtin.");

        var failure = PowerShellRecoveryHistory.Read(SessionState).FirstOrDefault(error => Id is null || Id == error.Id)
            ?? throw new PSInvalidOperationException(Id is null
                ? "No Azure PowerShell errors are available in the current session."
                : "That recovery ID is not in this session. Use Get-AzRecoveryId to list available errors.");

        var style = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, Style);
        if (!ShouldProcess(failure.Id, "Send Azure PowerShell diagnostic metadata to Azure AI"))
            return;

        var diagnostic = DescribeError(failure.Error);
        var ui = style.IsInteractive ? Host.UI : null;
        if (style.IsInteractive && !ConfirmInteractive(ui, diagnostic))
            return;

        var endpointWarnings = new List<string>();
        var (endpoint, accessToken) = ResolveModelAsync(options, endpointWarnings.Add, _cts.Token).GetAwaiter().GetResult();
        foreach (var warning in endpointWarnings)
            WriteWarning(warning);

        List<RecoveryMessage> messages =
        [
            new("user", diagnostic),
        ];
        while (true)
        {
            var answer = SendAsync(s_http, endpoint, options, accessToken, messages, style, _cts.Token).GetAwaiter().GetResult();
            var followUp = WriteAnswerAndReadFollowUp(answer, ui, WriteObject);
            if (followUp is null)
                return;
            messages.Add(new("assistant", answer));
            messages.Add(new("user", followUp));
        }
    }

    internal static string DescribeError(ErrorRecord error)
    {
        var invocation = error.InvocationInfo;
        var cmdlet = (CmdletInfo)invocation.MyCommand;
        var parameterNames = invocation.BoundParameters.Keys.ToList();
        var bindingError = error.Exception as ParameterBindingException;
        var kind = bindingError?.ErrorId switch
        {
            "AmbiguousParameterSet" => "Conflicting or incomplete parameter set",
            "NamedParameterNotFound" => "Unknown parameter",
            "MissingMandatoryParameter" => "Missing mandatory parameter",
            _ when bindingError is not null => "Parameter binding failed",
            _ => null,
        };
        if (kind is not null && invocation.Statement is { Length: > 0 } statement)
        {
            // Binding can fail before PowerShell populates BoundParameters. Read names, never values.
            var ast = Parser.ParseInput(statement, out _, out _);
            if (ast.EndBlock?.Statements.FirstOrDefault() is PipelineAst pipeline &&
                pipeline.PipelineElements.FirstOrDefault() is CommandAst command)
            {
                parameterNames.AddRange(command.CommandElements.OfType<CommandParameterAst>()
                    .Select(parameter => parameter.ParameterName));
            }
        }

        // Only emit metadata from the cmdlet definition and fixed classifications, not error text.
        var metadata = cmdlet.Parameters;
        var parameters = parameterNames.Select(name => FindParameterName(metadata, name))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(name => $"-{name}")
            .Order(StringComparer.Ordinal);
        return $"Command: {cmdlet.Name}\nError category: {error.CategoryInfo.Category}" +
            (kind is null ? "" : $"\nBinding error: {kind}") +
            $"\nRecognized parameters: {string.Join(", ", parameters)}";
    }

    private static string? FindParameterName(Dictionary<string, ParameterMetadata> metadata, string name)
    {
        var exact = metadata.Values.FirstOrDefault(parameter =>
            parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            parameter.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
        if (exact is not null)
            return exact.Name;

        var matches = metadata.Values.Where(parameter => parameter.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
            parameter.Aliases.Any(alias => alias.StartsWith(name, StringComparison.OrdinalIgnoreCase))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0].Name : null;
    }

    internal static bool ConfirmInteractive(PSHostUserInterface? ui, string diagnostic)
    {
        const string unsupported = "Interactive recovery requires a host that can read input. Use -Style oneshot instead.";
        if (ui is null)
            throw new PSInvalidOperationException(unsupported);

        string? answer;
        try
        {
            ui.WriteLine("Azure AI will receive this diagnostic:");
            ui.WriteLine(diagnostic);
            ui.WriteLine("Follow-ups are sent as typed and are not redacted. Do not include secrets or sensitive data.");
            ui.Write("Start interactive recovery? [y/N]: ");
            answer = ui.ReadLine();
        }
        catch (Exception ex) when (ex is PSInvalidOperationException or HostException or NotSupportedException)
        {
            throw new PSInvalidOperationException(unsupported, ex);
        }

        return answer is null
            ? throw new PSInvalidOperationException(unsupported)
            : string.Equals(answer.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? WriteAnswerAndReadFollowUp(string answer, PSHostUserInterface? ui, Action<string> writeOutput)
    {
        ui?.WriteLine(answer);
        writeOutput(answer);
        if (ui is null)
            return null;

        ui.Write("Follow-up (Enter to finish): ");
        var followUp = ui.ReadLine();
        return string.IsNullOrWhiteSpace(followUp) ? null : followUp;
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

    internal static async Task<string> SendAsync(HttpClient http, string endpoint, AgentOptions options,
        string accessToken, IReadOnlyList<RecoveryMessage> messages, RecoverySessionSettings style, CancellationToken token)
    {
        var promptName = $"RecoveryPrompts.{style.SessionStyle}.md";
        using var prompt = new StreamReader(typeof(SendRecoveryCmdlet).Assembly.GetManifestResourceStream(promptName)
            ?? throw new InvalidOperationException($"The embedded PowerShell recovery prompt '{promptName}' was not found."));
        var url = $"{endpoint.TrimEnd('/')}/responses?api-version={Uri.EscapeDataString(options.Builtin.ApiVersion)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            model = options.Builtin.ModelId,
            reasoning = new { effort = options.ReasoningEffort.ToLowerInvariant() },
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
