// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsResources;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Common.Recording;
using Tests.Common.Stubs;
using Tests.Recording;
using DeploymentCmdlet = CmdletsResources::Microsoft.Azure.Azclips.Cmdlets.Resources.NewResourceGroupDeploymentCmdlet;
using ResourcesExtensionIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class NewResourceGroupDeploymentCmdletTests
{
    private const string CommandName = "New-AzResourceGroupDeployment";
    private const string SubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string ResourceGroupName = "rg-shouldprocess";
    private const string DeploymentName = "deployment-shouldprocess";
    private const string TemplateUri = "https://example.invalid/templates/main.json";
    private const string DeploymentId = "/subscriptions/" + SubscriptionId
        + "/resourcegroups/" + ResourceGroupName
        + "/providers/Microsoft.Resources/deployments/" + DeploymentName;
    private const string DeploymentUri = "https://management.azure.com" + DeploymentId + "?api-version=2025-04-01";

    public static TheoryData<string, bool?, bool?, bool> WhatIfCases
    {
        get
        {
            var cases = new TheoryData<string, bool?, bool?, bool>();
            foreach (string source in new[] { "linked", "json", "bicep", "missing-json", "missing-bicep" })
            {
                cases.Add(source, true, null, false);
                cases.Add(source, true, false, false);
                cases.Add(source, null, null, true);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(WhatIfCases))]
    public void WhatIf_SuppressesSessionsTemplateLoadingAndAllHttp(
        string source, bool? whatIf, bool? confirm, bool whatIfPreference)
    {
        using var fixture = new DeploymentFixture();
        var parameters = LinkedTemplateParameters();
        if (source != "linked")
        {
            parameters.Remove("TemplateUri");
            string extension = source.EndsWith("bicep", StringComparison.Ordinal) ? ".bicep" : ".json";
            parameters["TemplateFile"] = fixture.TemplatePath(
                extension, create: !source.StartsWith("missing-", StringComparison.Ordinal));
        }
        if (whatIf.HasValue)
        {
            parameters["WhatIf"] = whatIf.Value;
        }
        if (confirm.HasValue)
        {
            parameters["Confirm"] = confirm.Value;
        }

        var result = fixture.Invoke(parameters, whatIfPreference: whatIfPreference);

        AssertNoErrors(result);
        AssertNoExecution(fixture, result);
        Assert.Empty(fixture.Host.RecordingUI.Prompts);
        Assert.Contains(DeploymentName, fixture.Host.RecordingUI.Text, StringComparison.Ordinal);
        Assert.Contains(ResourceGroupName, fixture.Host.RecordingUI.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, ConfirmImpact.High, false)]
    [InlineData(true, ConfirmImpact.High, true)]
    [InlineData(false, ConfirmImpact.Medium, false)]
    [InlineData(false, ConfirmImpact.Medium, true)]
    [InlineData(false, ConfirmImpact.Low, false)]
    [InlineData(false, ConfirmImpact.Low, true)]
    public void Confirmation_ControlsSessionOpening(
        bool explicitConfirm, ConfirmImpact confirmPreference, bool approve)
    {
        using var fixture = new DeploymentFixture(approve);
        var parameters = LinkedTemplateParameters();
        if (explicitConfirm)
        {
            parameters["Confirm"] = true;
        }

        var result = fixture.Invoke(parameters, confirmPreference: confirmPreference);

        AssertNoErrors(result);
        string prompt = Assert.Single(fixture.Host.RecordingUI.Prompts);
        Assert.Contains(DeploymentName, prompt, StringComparison.Ordinal);
        Assert.Contains(ResourceGroupName, prompt, StringComparison.Ordinal);
        if (approve)
        {
            AssertDeploymentExecuted(fixture, result);
        }
        else
        {
            AssertNoExecution(fixture, result);
        }
    }

    [Theory]
    [InlineData(ConfirmImpact.Medium)]
    [InlineData(ConfirmImpact.Low)]
    public void ConfirmFalse_OverridesPromptingPreference(ConfirmImpact confirmPreference)
    {
        using var fixture = new DeploymentFixture(approve: false);
        var parameters = LinkedTemplateParameters();
        parameters["Confirm"] = false;

        var result = fixture.Invoke(parameters, confirmPreference: confirmPreference);

        AssertDeploymentExecuted(fixture, result);
        Assert.Empty(fixture.Host.RecordingUI.Prompts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WhatIfFalse_OverridesAmbientWhatIfPreference(bool explicitConfirm)
    {
        using var fixture = new DeploymentFixture(approve: true);
        var parameters = LinkedTemplateParameters();
        parameters["WhatIf"] = false;
        if (explicitConfirm)
        {
            parameters["Confirm"] = true;
        }

        var result = fixture.Invoke(parameters, whatIfPreference: true);

        AssertDeploymentExecuted(fixture, result);
        Assert.Equal(explicitConfirm ? 1 : 0, fixture.Host.RecordingUI.Prompts.Count);
    }

    [Fact]
    public void OrdinaryInvocation_SendsDeploymentPutAndReturnsTypedOutput()
    {
        using var fixture = new DeploymentFixture();
        var parameters = LinkedTemplateParameters();
        parameters["ParametersUri"] = "https://example.invalid/templates/parameters.json";
        parameters["QueryString"] = "?fixture=true";
        parameters["RollbackOnError"] = true;
        parameters["ValidationLevel"] = "Template";

        var result = fixture.Invoke(parameters);

        var put = AssertDeploymentExecuted(fixture, result);
        using var body = JsonDocument.Parse(put.Body!);
        var properties = body.RootElement.GetProperty("properties");
        Assert.Equal("fixture=true", properties.GetProperty("templateLink").GetProperty("queryString").GetString());
        Assert.Equal("https://example.invalid/templates/parameters.json?fixture=true",
            properties.GetProperty("parametersLink").GetProperty("uri").GetString());
        Assert.False(properties.TryGetProperty("parameters", out _));
        Assert.Equal("LastSuccessful", properties.GetProperty("onErrorDeployment").GetProperty("type").GetString());
        Assert.Equal("Template", properties.GetProperty("validationLevel").GetString());
        Assert.Empty(fixture.Host.RecordingUI.Prompts);
    }

    [Theory]
    [InlineData("invalid-name", "Deployment name must be")]
    [InlineData("conflicting-templates", "Exactly one of TemplateUri, TemplateFile, or TemplateSpecId")]
    [InlineData("missing-template", "Exactly one of TemplateUri, TemplateFile, or TemplateSpecId")]
    [InlineData("invalid-mode", "Mode must be either")]
    [InlineData("missing-json", "File not found:")]
    [InlineData("missing-bicep", "File not found:")]
    public void ExecutingInvocation_PreservesValidationErrors(string scenario, string expectedError)
    {
        using var fixture = new DeploymentFixture();
        var parameters = LinkedTemplateParameters();
        switch (scenario)
        {
            case "invalid-name":
                parameters["Name"] = "invalid/deployment";
                break;
            case "conflicting-templates":
                parameters["TemplateFile"] = fixture.TemplatePath(".json");
                break;
            case "missing-template":
                parameters.Remove("TemplateUri");
                break;
            case "invalid-mode":
                parameters["Mode"] = "Replace";
                break;
            case "missing-json":
            case "missing-bicep":
                parameters.Remove("TemplateUri");
                parameters["TemplateFile"] = fixture.TemplatePath(scenario == "missing-json" ? ".json" : ".bicep");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        var result = fixture.Invoke(parameters);

        AssertPowerShellError(result, expectedError);
        Assert.Equal(1, fixture.Engine.ExecutionSessionsOpened);
        Assert.Equal(0, fixture.Engine.DiscoverySessionsOpened);
        Assert.Empty(fixture.Interceptor.Captured);
        Assert.Equal(0, fixture.NetworkGuard.RequestCount);
        Assert.Empty(fixture.Host.RecordingUI.Prompts);
    }

    [Fact]
    public void ExecutingInvocation_SurfacesHttpFailure()
    {
        using var fixture = new DeploymentFixture(responder: request => JsonResponse(
            request, HttpStatusCode.BadRequest,
            """{"error":{"code":"InvalidTemplateDeployment","message":"Canned deployment rejection"}}"""));

        var result = fixture.Invoke(LinkedTemplateParameters());

        AssertPowerShellError(result, "Canned deployment rejection");
        Assert.Equal(1, fixture.Engine.ExecutionSessionsOpened);
        Assert.Equal(0, fixture.Engine.DiscoverySessionsOpened);
        var put = Assert.Single(fixture.Interceptor.Captured);
        Assert.Equal("PUT", put.Method);
        Assert.Equal(DeploymentUri, put.Uri);
        Assert.Equal(0, fixture.NetworkGuard.RequestCount);
        Assert.Empty(fixture.Host.RecordingUI.Prompts);
    }

    private static Dictionary<string, object> LinkedTemplateParameters() => new()
    {
        ["Name"] = DeploymentName,
        ["ResourceGroupName"] = ResourceGroupName,
        ["SubscriptionId"] = SubscriptionId,
        ["TemplateUri"] = TemplateUri,
    };

    private static void AssertNoErrors(InvocationResult result)
    {
        Assert.Null(result.TerminatingError);
        Assert.False(result.HadErrors, result.ErrorText);
        Assert.Empty(result.Errors);
    }

    private static void AssertNoExecution(DeploymentFixture fixture, InvocationResult result)
    {
        Assert.Equal(0, fixture.Engine.ExecutionSessionsOpened);
        Assert.Equal(0, fixture.Engine.DiscoverySessionsOpened);
        Assert.Empty(fixture.Interceptor.Captured);
        Assert.Equal(0, fixture.NetworkGuard.RequestCount);
        Assert.Empty(result.Output);
    }

    private static CapturedHttpRequest AssertDeploymentExecuted(DeploymentFixture fixture, InvocationResult result)
    {
        AssertNoErrors(result);
        Assert.Equal(1, fixture.Engine.ExecutionSessionsOpened);
        Assert.Equal(0, fixture.Engine.DiscoverySessionsOpened);
        Assert.Equal(0, fixture.NetworkGuard.RequestCount);
        var put = Assert.Single(fixture.Interceptor.Captured.Where(request => request.Method == "PUT"));
        Assert.All(fixture.Interceptor.Captured, request =>
        {
            Assert.Equal(DeploymentUri, request.Uri);
            Assert.Contains(request.Method, new[] { "PUT", "GET" });
        });
        using var body = JsonDocument.Parse(put.Body!);
        var properties = body.RootElement.GetProperty("properties");
        Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
        Assert.Equal(TemplateUri, properties.GetProperty("templateLink").GetProperty("uri").GetString());
        Assert.False(properties.TryGetProperty("template", out _));

        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(result.Output).BaseObject);
        Assert.Equal(DeploymentId, deployment.Id);
        Assert.Equal(DeploymentName, deployment.Name);
        Assert.Equal("Microsoft.Resources/deployments", deployment.Type);
        Assert.NotNull(deployment.Properties);
        Assert.Equal("Succeeded", deployment.Properties.ProvisioningState);
        return put;
    }

    private static void AssertPowerShellError(InvocationResult result, string expectedError)
    {
        // Session failures can both call WriteError and throw a terminating exception.
        Assert.True(result.HadErrors || result.TerminatingError is not null, "Expected a PowerShell error.");
        Assert.Contains(expectedError, result.ErrorText, StringComparison.Ordinal);
        Assert.Empty(result.Output);
    }

    private static HttpResponseMessage SuccessResponse(HttpRequestMessage request)
    {
        if (request.RequestUri?.AbsoluteUri != DeploymentUri
            || (request.Method != HttpMethod.Put && request.Method != HttpMethod.Get))
        {
            throw new InvalidOperationException($"Unexpected HTTP request: {request.Method} {request.RequestUri}");
        }

        return JsonResponse(request, HttpStatusCode.OK, $$"""
            {
              "id": "{{DeploymentId}}",
              "name": "{{DeploymentName}}",
              "type": "Microsoft.Resources/deployments",
              "properties": { "provisioningState": "Succeeded", "mode": "Incremental" }
            }
            """);
    }

    private static HttpResponseMessage JsonResponse(HttpRequestMessage request, HttpStatusCode status, string json)
    {
        var response = new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }

    private sealed record InvocationResult(
        IReadOnlyList<PSObject> Output, IReadOnlyList<ErrorRecord> Errors,
        bool HadErrors, RuntimeException? TerminatingError)
    {
        public string ErrorText => string.Join(Environment.NewLine,
            Errors.Select(error => error.ToString()).Append(TerminatingError?.ToString() ?? string.Empty));
    }

    private sealed class DeploymentFixture : IDisposable
    {
        private readonly string _templateDirectory = Path.Combine(
            Path.GetTempPath(), $"azclips-deployment-cmdlet-{Guid.NewGuid():N}");
        private readonly HttpClient _httpClient;
        private Runspace? _runspace;

        public RecordingHost Host { get; }
        public CountingEngine Engine { get; }
        public CapturingTracingInterceptor Interceptor { get; }
        public RejectNetworkHandler NetworkGuard { get; } = new();

        public DeploymentFixture(bool approve = false, Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
        {
            Host = new RecordingHost(approve);
            Interceptor = new CapturingTracingInterceptor(responder ?? SuccessResponse);
            _httpClient = new HttpClient(NetworkGuard);
            try
            {
                var configuration = new Mock<IConfigurationService>(MockBehavior.Strict);
                configuration.Setup(service => service.GetConfiguration<CoreOptions>(
                    It.IsAny<string?>(), It.IsAny<CoreOptions?>())).Returns(() => new CoreOptions());
                configuration.Setup(service => service.GetConfiguration<BenchmarkOptions>(
                    It.IsAny<string?>(), It.IsAny<BenchmarkOptions?>())).Returns(() => new BenchmarkOptions());

                AzclipsServiceProvider.Dispose();
                AzclipsServiceProvider.OverrideRegistration(services =>
                {
                    services.AddSingleton(configuration.Object);
                    services.AddSingleton<ITelemetryService, StubTelemetryService>();
                    services.AddSingleton<ILoggingService, NullLoggingService>();
                    services.AddSingleton<IAuthService, StubAuthService>();
                    services.AddSingleton<IProfileService>(new StubProfileService(seedDefault: false));
                    services.AddSingleton<IAssemblyLoadingProvider>(
                        new StubExtensionLoader(typeof(ResourcesExtensionIndex).Assembly, "Resources"));
                    services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                    services.AddSingleton<ITracingInterceptor>(Interceptor);
                    services.AddSingleton(_httpClient);
                    services.AddSingleton<IEngine>(provider =>
                        new CountingEngine(ActivatorUtilities.CreateInstance<Core.Engine.Engine>(provider)));
                });
                AzclipsServiceProvider.Initialize(ClientType.PowerShell);
                Engine = (CountingEngine)AzclipsServiceProvider.GetRequiredService<IEngine>();

                var initialState = InitialSessionState.CreateDefault2();
                initialState.Commands.Add(new SessionStateCmdletEntry(CommandName, typeof(DeploymentCmdlet), null));
                _runspace = RunspaceFactory.CreateRunspace(Host, initialState);
                _runspace.Open();
                _runspace.SessionStateProxy.SetVariable("PSModuleAutoLoadingPreference", "None");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string TemplatePath(string extension, bool create = false)
        {
            string path = Path.Combine(_templateDirectory, "main" + extension);
            if (create)
            {
                Directory.CreateDirectory(_templateDirectory);
                File.WriteAllText(path, extension == ".bicep"
                    ? "targetScope = 'resourceGroup'\noutput deployed bool = true\n"
                    : """
                      {
                        "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
                        "contentVersion": "1.0.0.0",
                        "resources": []
                      }
                      """);
            }
            return path;
        }

        public InvocationResult Invoke(
            IDictionary<string, object> parameters, bool whatIfPreference = false,
            ConfirmImpact confirmPreference = ConfirmImpact.High)
        {
            var runspace = _runspace ?? throw new ObjectDisposedException(nameof(DeploymentFixture));
            runspace.SessionStateProxy.SetVariable("WhatIfPreference", whatIfPreference);
            runspace.SessionStateProxy.SetVariable("ConfirmPreference", confirmPreference);
            using var ps = PowerShell.Create();
            ps.Runspace = runspace;
            ps.AddCommand(CommandName);
            foreach (var (name, value) in parameters)
            {
                ps.AddParameter(name, value);
            }

            Collection<PSObject> output = [];
            RuntimeException? terminatingError = null;
            try
            {
                output = ps.Invoke();
            }
            catch (RuntimeException exception)
            {
                terminatingError = exception;
            }

            return new InvocationResult(output.ToArray(), ps.Streams.Error.ToArray(), ps.HadErrors, terminatingError);
        }

        public void Dispose()
        {
            try
            {
                _runspace?.Dispose();
            }
            finally
            {
                try
                {
                    AzclipsServiceProvider.Dispose();
                }
                finally
                {
                    _httpClient.Dispose();
                    if (Directory.Exists(_templateDirectory))
                    {
                        Directory.Delete(_templateDirectory, recursive: true);
                    }
                }
            }
        }
    }

    private sealed class CountingEngine(IEngine inner) : IEngine
    {
        public int ExecutionSessionsOpened { get; private set; }
        public int DiscoverySessionsOpened { get; private set; }

        public ISession OpenExecutionSession(
            CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default)
        {
            ExecutionSessionsOpened++;
            return inner.OpenExecutionSession(invocation, outputStreams, cancellationToken);
        }

        public ISession OpenDiscoverySession(
            IList<string> extensionNames, string? commandName, OutputStreams outputStreams,
            bool isTransient = false, CancellationToken cancellationToken = default)
        {
            DiscoverySessionsOpened++;
            return inner.OpenDiscoverySession(extensionNames, commandName, outputStreams, isTransient, cancellationToken);
        }

        public void Dispose() => inner.Dispose();
    }

    private sealed class RejectNetworkHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new InvalidOperationException($"HTTP escaped the test interceptor: {request.Method} {request.RequestUri}");
        }
    }

    private sealed class RecordingHost(bool approve) : PSHost
    {
        public RecordingUserInterface RecordingUI { get; } = new(approve);
        public override PSHostUserInterface UI => RecordingUI;
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name => nameof(NewResourceGroupDeploymentCmdletTests);
        public override Version Version { get; } = new(1, 0);
        public override CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public override CultureInfo CurrentUICulture => CultureInfo.GetCultureInfo("en-US");
        public override void EnterNestedPrompt() => throw new NotSupportedException();
        public override void ExitNestedPrompt() => throw new NotSupportedException();
        public override void SetShouldExit(int exitCode) => throw new NotSupportedException();
        public override void NotifyBeginApplication() { }
        public override void NotifyEndApplication() { }
    }

    private sealed class RecordingUserInterface(bool approve) : PSHostUserInterface
    {
        private readonly StringBuilder _text = new();
        public string Text => _text.ToString();
        public List<string> Prompts { get; } = [];
        public override PSHostRawUserInterface RawUI { get; } = Mock.Of<PSHostRawUserInterface>();

        public override int PromptForChoice(
            string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            Prompts.Add(message);
            string answer = approve ? "Yes" : "No";
            for (int index = 0; index < choices.Count; index++)
            {
                if (choices[index].Label.Replace("&", string.Empty).Equals(answer, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            throw new InvalidOperationException($"Confirmation prompt did not offer '{answer}'.");
        }

        public override void Write(string value) => _text.Append(value);
        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) => Write(value);
        public override void WriteLine(string value) => _text.AppendLine(value);
        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) => WriteLine(value);
        public override void WriteErrorLine(string value) => WriteLine(value);
        public override void WriteDebugLine(string message) => WriteLine(message);
        public override void WriteVerboseLine(string message) => WriteLine(message);
        public override void WriteWarningLine(string message) => WriteLine(message);
        public override void WriteProgress(long sourceId, ProgressRecord record) { }
        public override string ReadLine() => throw new NotSupportedException();
        public override SecureString ReadLineAsSecureString() => throw new NotSupportedException();
        public override Dictionary<string, PSObject> Prompt(
            string caption, string message, Collection<FieldDescription> descriptions) => throw new NotSupportedException();
        public override PSCredential PromptForCredential(
            string caption, string message, string userName, string targetName) => throw new NotSupportedException();
        public override PSCredential PromptForCredential(
            string caption, string message, string userName, string targetName,
            PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options) => throw new NotSupportedException();
    }
}
