// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;

namespace Cmdlets.Tests;

public class RecoveryCmdletTests
{
    [Fact]
    public void GetRecoveryId_ListsOnlyAzclipsErrorsNewestFirstWithStableIds()
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
            try { Connect-AzAccount -FirstBadParameter } catch {}
            try { Send-AzRecovery -Id invalid-id } catch {
                if ($_.Exception.Message -notmatch 'Invalid recovery ID') { throw }
            }
            try { Get-Item -SecondBadParameter } catch {}
            try { Connect-AzAccount -ThirdBadParameter } catch {}
            function Get-AzPretend { throw 'Not an AzCLIPS cmdlet' }
            try { Get-AzPretend } catch {}
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
        Assert.Empty(ps.AddScript("$Error.Clear(); Get-AzRecoveryId").Invoke<RecoveryError>());
    }

    [Theory]
    [InlineData("interactive", "OnEsHoT", "without asking follow-up questions")]
    [InlineData("oneshot", "interactive", "Ask one focused follow-up question")]
    [InlineData("interactive", null, "Ask one focused follow-up question")]
    [InlineData("oneshot", null, "without asking follow-up questions")]
    public async Task SendRecovery_PostsDiagnosticWithSelectedPowerShellPrompt(
        string configuredStyle, string? styleOverride, string expectedInstruction)
    {
        using var http = new HttpClient(new ModelHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.invalid/openai/responses?api-version=2025-04-01-preview", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("fake-token", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.False(body.RootElement.GetProperty("store").GetBoolean());
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

        var options = new AgentOptions { Recovery = new RecoveryOptions { Style = configuredStyle } };
        var style = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride);
        var answer = await SendRecoveryCmdlet.SendAsync(http, "https://example.invalid/openai/", options.Builtin,
            "fake-token", [new("user", "sample diagnostic")], style, CancellationToken.None);

        Assert.Equal("Suggested fix", answer);
    }

    private sealed class ModelHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
