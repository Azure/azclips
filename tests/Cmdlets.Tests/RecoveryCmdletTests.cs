// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Moq;

namespace Cmdlets.Tests;

public class RecoveryCmdletTests
{
    [Theory]
    [InlineData("off")]
    [InlineData(null)]
    public async Task GetRecoveryId_ListsAzclipsErrorsWithStableIdsUntilConfigurationReset(string? resetValue)
    {
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry("Connect-AzAccount", typeof(ConnectAccountCmdlet), null));
        state.Commands.Add(new SessionStateCmdletEntry("Get-AzRecoveryId", typeof(GetRecoveryIdCmdlet), null));
        state.Commands.Add(new SessionStateCmdletEntry("Send-AzRecovery", typeof(SendRecoveryCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;

        var errors = ps.AddScript("""
            try { Connect-AzAccount -FirstBadParameter } catch { if ($_.FullyQualifiedErrorId -notlike 'NamedParameterNotFound,*') { throw } }
            try { Send-AzRecovery -Id invalid-id } catch {
                if ($_.Exception.Message -notmatch 'Invalid recovery ID') { throw }
            }
            try { Get-Item -SecondBadParameter } catch { if ($_.FullyQualifiedErrorId -notlike 'NamedParameterNotFound,*') { throw } }
            try { Connect-AzAccount -ThirdBadParameter } catch { if ($_.FullyQualifiedErrorId -notlike 'NamedParameterNotFound,*') { throw } }
            function Get-AzPretend { throw 'Not an AzCLIPS cmdlet' }
            try { Get-AzPretend } catch { if ($_.Exception.Message -ne 'Not an AzCLIPS cmdlet') { throw } }
            Get-AzRecoveryId
            Get-AzRecoveryId
            """).Invoke<RecoveryError>();

        Assert.Equal(4, errors.Count);
        Assert.Contains("ThirdBadParameter", errors[0].Error.Exception.Message);
        Assert.Contains("FirstBadParameter", errors[1].Error.Exception.Message);
        Assert.All(errors, error =>
        {
            Assert.True(FailureId.TryParse(error.Id, out var id));
            Assert.Equal((uint)Environment.ProcessId, id.TerminalSessionId);
        });
        Assert.NotEqual(errors[0].Id, errors[1].Id);
        Assert.Equal(errors[0].Id, errors[2].Id);
        Assert.Equal(errors[1].Id, errors[3].Id);

        ps.Commands.Clear();
        var configChange = new RecoveryConfigurationChangeHandler(Mock.Of<IFailureHistoryStore>());
        await configChange.OnChangedAsync(RecoveryConfigurationChangeHandler.TriggerKey, resetValue, CancellationToken.None);
        Assert.Empty(ps.AddScript("Get-AzRecoveryId").Invoke<RecoveryError>());

        ps.Commands.Clear();
        Assert.True(Assert.Single(ps.AddScript("$Error.Count -gt 0").Invoke<bool>()));

        ps.Commands.Clear();
        var current = Assert.Single(ps.AddScript("""
            try { Connect-AzAccount -AfterReset } catch { if ($_.FullyQualifiedErrorId -notlike 'NamedParameterNotFound,*') { throw } }
            Get-AzRecoveryId
            """).Invoke<RecoveryError>());
        Assert.DoesNotContain(errors, error => error.Id == current.Id);
        Assert.Contains("AfterReset", current.Error.Exception.Message);
    }

    [Fact]
    public void DescribeError_IncludesConflictingParameterNamesWithoutValues()
    {
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry("Get-AzProfile", typeof(GetProfileCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        var error = Assert.Single(ps.AddScript("""
            try { Get-AzProfile -Active -Name 'https://example.invalid/?sig=fake-secret' } catch { $_ }
            """).Invoke<ErrorRecord>());
        error.ErrorDetails = new ErrorDetails("Request URI: https://example.invalid/?sig=fake-secret");

        var diagnostic = SendRecoveryCmdlet.DescribeError(error);
        Assert.Contains("Conflicting or incomplete parameter set", diagnostic);
        Assert.Contains("-Active, -Name", diagnostic);
        Assert.DoesNotContain("example.invalid", diagnostic);
        Assert.DoesNotContain("fake-secret", diagnostic);
    }

    [Fact]
    public void ConfirmInteractive_RejectsHostThatCannotReadInput()
    {
        var ui = new Mock<PSHostUserInterface>();
        ui.Setup(host => host.ReadLine()).Throws(new PSInvalidOperationException("Noninteractive host"));
        var error = Assert.Throws<PSInvalidOperationException>(() => SendRecoveryCmdlet.ConfirmInteractive(ui.Object, "metadata"));
        Assert.Contains("-Style oneshot", error.Message);
    }

    [Fact]
    public void InteractiveAnswer_WritesToHostAndPipelineBeforeReadingInput()
    {
        var events = new List<string>();
        var ui = new Mock<PSHostUserInterface>();
        ui.Setup(host => host.WriteLine("Advice")).Callback(() => events.Add("host"));
        ui.Setup(host => host.ReadLine()).Returns(() =>
        {
            events.Add("read");
            return "next";
        });

        Assert.Equal("next", SendRecoveryCmdlet.WriteAnswerAndReadFollowUp("Advice", ui.Object, answer => events.Add(answer)));
        Assert.Equal(["host", "Advice", "read"], events);
    }

    [Theory]
    [InlineData("interactive", "OnEsHoT", "without asking follow-up questions", "low")]
    [InlineData("oneshot", "interactive", "Ask one focused follow-up question", "high")]
    [InlineData("interactive", null, "Ask one focused follow-up question", "medium")]
    [InlineData("oneshot", null, "without asking follow-up questions", "medium")]
    public async Task SendRecovery_PostsDiagnosticWithSelectedPowerShellPrompt(
        string configuredStyle, string? styleOverride, string expectedInstruction, string effort)
    {
        using var http = new HttpClient(new ModelHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.invalid/openai/responses?api-version=2025-04-01-preview", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("fake-token", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.False(body.RootElement.GetProperty("store").GetBoolean());
            Assert.Equal(effort, body.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.Equal("sample diagnostic", body.RootElement.GetProperty("input")[0].GetProperty("content").GetString());
            var instructions = body.RootElement.GetProperty("instructions").GetString();
            Assert.Contains("AzCLIPS Azure PowerShell module", instructions);
            Assert.Contains(expectedInstruction, instructions);
            Assert.DoesNotContain("submit_response", instructions);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"Suggested fix"}]}]}
                    """, Encoding.UTF8, "application/json"),
            };
        }));

        var options = new AgentOptions { Recovery = new RecoveryOptions { Style = configuredStyle }, ReasoningEffort = effort };
        var style = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride);
        var answer = await SendRecoveryCmdlet.SendAsync(http, "https://example.invalid/openai/", options,
            "fake-token", [new("user", "sample diagnostic")], style, CancellationToken.None);

        Assert.Equal("Suggested fix", answer);
    }

    private sealed class ModelHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
