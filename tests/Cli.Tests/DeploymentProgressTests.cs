// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// <c>-Verbose</c> resource progress for New-AzResourceGroupDeployment and New-AzManagementGroupDeployment (#910): the
/// PowerShell-only processors list the deployment's operations while it runs and write each new provisioning state, like
/// Az PowerShell; the CLI processors never read the operations.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class DeploymentProgressTests : IDisposable
{
    private const string AsgType = "Microsoft.Network/applicationSecurityGroups";

    private readonly CapturingLogger logger = new();

    public DeploymentProgressTests()
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton<ILoggingService>(logger));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"));
    }

    public void Dispose() => AzclipsServiceProvider.Dispose();

    [Fact]
    public async Task ResourceGroup_PowerShell_ReportsEachNewStateOnce_ThenCatchesUpWhenTheDeploymentFinishes()
    {
        var arm = new FakeArm
        {
            ReleaseDeploymentAfterOperationsCalls = 2,
            Operations = call => call switch
            {
                1 => OperationsPage(Op("1", "Running", "asg-1")),
                2 => OperationsPage(Op("1", "Running", "asg-1"), Op("2", "Running", "asg-2")),
                _ => OperationsPage(Op("1", "Succeeded", "asg-1"), Op("2", "Succeeded", "asg-2")),
            },
        };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(new NewResourceGroupDeploymentProcessor_PSOutput(), arm, output, ImmediateChecks(2));

        await create.ProcessAsync();

        Assert.Equal(
            [
                $"Resource {AsgType} 'asg-1' provisioning status is running",
                $"Resource {AsgType} 'asg-2' provisioning status is running",
                $"Resource {AsgType} 'asg-1' provisioning status is succeeded",
                $"Resource {AsgType} 'asg-2' provisioning status is succeeded",
            ],
            logger.ResourceLines);
        Assert.Contains("Checking deployment status in 5 seconds", logger.VerboseLines);
        Assert.Equal(3, arm.OperationsCalls);
        Assert.All(arm.OperationsRequests, uri => Assert.Equal(DeploymentUri.Replace("/providers/Microsoft.Resources", "") + "/operations?api-version=2026-06-01", uri));
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ResourceGroup_PowerShell_ReadsEveryPageOfOperations()
    {
        var arm = new FakeArm
        {
            ReleaseDeploymentAfterOperationsCalls = 2,
            Operations = call => call % 2 == 1
                ? OperationsPageWithNextLink("https://management.azure.com/next-page?skiptoken=2", Op("1", "Succeeded", "asg-1"))
                : OperationsPage(Op("2", "Succeeded", "asg-2")),
        };
        var create = ResourceGroupCreate(new NewResourceGroupDeploymentProcessor_PSOutput(), arm, new FakeOutputHandler(), ImmediateChecks(1));

        await create.ProcessAsync();

        Assert.Equal(
            [
                $"Resource {AsgType} 'asg-1' provisioning status is succeeded",
                $"Resource {AsgType} 'asg-2' provisioning status is succeeded",
            ],
            logger.ResourceLines);
        Assert.Contains("https://management.azure.com/next-page?skiptoken=2", arm.OperationsRequests);
    }

    [Fact]
    public async Task ResourceGroup_PowerShell_SkipsFailedOperationsAndOperationsWithoutATarget_AndKeepsTheDeploymentFailure()
    {
        var arm = new FakeArm
        {
            ReleaseDeploymentAfterOperationsCalls = 1,
            DeploymentState = "Failed",
            Operations = _ => OperationsPage(Op("1", "Succeeded", "asg-1"), Op("2", "Failed", "asg-2"), Op("3", "Running", null)),
        };
        var create = ResourceGroupCreate(new NewResourceGroupDeploymentProcessor_PSOutput(), arm, new FakeOutputHandler(), ImmediateChecks(1));

        Exception error = await Assert.ThrowsAnyAsync<Exception>(create.ProcessAsync);

        Assert.StartsWith("Long running operation failed", error.Message);
        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], logger.ResourceLines);
    }

    [Fact]
    public async Task ResourceGroup_PowerShell_FailedOperationsRead_StopsProgressWithoutAnErrorOrChangingTheResult()
    {
        var arm = new FakeArm
        {
            ReleaseDeploymentAfterOperationsCalls = 1,
            Operations = _ => Status(HttpStatusCode.Forbidden, """{"error":{"code":"AuthorizationFailed","message":"no read access"}}"""),
        };
        var publisher = new Mock<IEventPublisher>();
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(new NewResourceGroupDeploymentProcessor_PSOutput(), arm, output, ImmediateChecks(int.MaxValue), publisher);

        await create.ProcessAsync();

        Assert.Single(output.WrittenValues);
        Assert.Empty(logger.ResourceLines);
        Assert.Contains(logger.VerboseLines, line => line.StartsWith("Unable to read the deployment operations") && line.Contains("403"));
        // The first failure ends the checks, and no catch-up read follows it.
        Assert.Equal(1, arm.OperationsCalls);
        // A failed read must not publish ResourceOperationFailed, which PowerShell writes to the error stream.
        publisher.Verify(p => p.Publish(Events.ResourceOperationFailed, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResourceGroup_PowerShell_DeploymentFinishedBeforeTheFirstCheck_SendsNoOperationsRequest()
    {
        var arm = new FakeArm { Operations = _ => throw new InvalidOperationException("Operations must not be read.") };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(new NewResourceGroupDeploymentProcessor_PSOutput(), arm, output, ImmediateChecks(0));

        await create.ProcessAsync();

        Assert.Equal(0, arm.OperationsCalls);
        Assert.Empty(logger.ResourceLines);
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ResourceGroup_Cli_NeverReadsOperations()
    {
        var arm = new FakeArm { Operations = _ => throw new InvalidOperationException("Operations must not be read.") };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(new NewResourceGroupDeploymentProcessor_Orchestrator(), arm, output, ImmediateChecks(int.MaxValue));

        await create.ProcessAsync();

        Assert.Equal(0, arm.OperationsCalls);
        Assert.Empty(logger.VerboseLines);
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ManagementGroup_PowerShell_ReportsProgressFromTheManagementGroupOperations()
    {
        var arm = new FakeArm
        {
            ReleaseDeploymentAfterOperationsCalls = 1,
            Operations = _ => OperationsPage(Op("1", "Succeeded", "asg-1")),
        };
        var output = new FakeOutputHandler();
        var create = Init(new CreateManagementGroupDeploymentProcessor_PSOutput(), arm, output, null);
        create.ManagementGroupId = "mg1";
        create.Location = "eastus";
        create.Name = "dep";
        create.TemplateUri = "https://example.com/t.json";
        create.ProgressDelay = ImmediateChecks(1);

        await create.ProcessAsync();

        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], logger.ResourceLines);
        Assert.All(arm.OperationsRequests, uri => Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/mg1/providers/Microsoft.Resources/deployments/dep/operations?api-version=2026-06-01",
            uri));
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ManagementGroup_Cli_NeverReadsOperations()
    {
        var arm = new FakeArm { Operations = _ => throw new InvalidOperationException("Operations must not be read.") };
        var create = Init(new CreateManagementGroupDeploymentProcessor_Orchestrator(), arm, new FakeOutputHandler(), null);
        create.ManagementGroupId = "mg1";
        create.Location = "eastus";
        create.Name = "dep";
        create.TemplateUri = "https://example.com/t.json";
        create.ProgressDelay = ImmediateChecks(int.MaxValue);

        await create.ProcessAsync();

        Assert.Equal(0, arm.OperationsCalls);
        Assert.Empty(logger.VerboseLines);
    }

    private static T ResourceGroupCreate<T>(T processor, FakeArm arm, FakeOutputHandler output, Func<TimeSpan, CancellationToken, Task> delay, Mock<IEventPublisher>? publisher = null)
        where T : NewResourceGroupDeploymentProcessor_Orchestrator
    {
        Init(processor, arm, output, publisher);
        processor.SubscriptionId = SubscriptionId;
        processor.ResourceGroupName = "test-rg";
        processor.Name = "dep";
        processor.TemplateUri = "https://example.com/t.json";
        processor.ProgressDelay = delay;
        return processor;
    }

    // Progress reads run alongside the deployment's own polling, so the publisher must be thread-safe: a Moq mock is.
    private static T Init<T>(T processor, FakeArm arm, FakeOutputHandler output, Mock<IEventPublisher>? publisher)
        where T : IProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = SubscriptionId };
        processor.Init(new ProcessorContext(
            new FakeHttpPipeline((request, _) => arm.Respond(request)),
            (publisher ?? new Mock<IEventPublisher>()).Object,
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }

    // Lets the first `checks` progress checks run at once; later ones wait until the deployment finishes and stops them.
    private static Func<TimeSpan, CancellationToken, Task> ImmediateChecks(int checks)
    {
        int waits = 0;
        return (_, token) => Interlocked.Increment(ref waits) <= checks ? Task.CompletedTask : Task.Delay(Timeout.Infinite, token);
    }

    private static string Op(string id, string state, string? resourceName) => resourceName is null
        ? $$$"""{"operationId":"{{{id}}}","properties":{"provisioningState":"{{{state}}}"}}"""
        : $$$$"""{"operationId":"{{{{id}}}}","properties":{"provisioningState":"{{{{state}}}}","targetResource":{"resourceType":"{{{{AsgType}}}}","resourceName":"{{{{resourceName}}}}"}}}""";

    private static HttpResponseMessage OperationsPage(params string[] operations) =>
        Status(HttpStatusCode.OK, $$"""{"value":[{{string.Join(",", operations)}}]}""");

    private static HttpResponseMessage OperationsPageWithNextLink(string nextLink, params string[] operations) =>
        Status(HttpStatusCode.OK, $$"""{"value":[{{string.Join(",", operations)}}],"nextLink":"{{nextLink}}"}""");

    /// <summary>
    /// ARM for one deployment: validate succeeds, the PUT starts it, and its GETs report <see cref="DeploymentState"/> only once
    /// the operations have been read <see cref="ReleaseDeploymentAfterOperationsCalls"/> times, so tests decide how many
    /// progress checks happen while it runs.
    /// </summary>
    private sealed class FakeArm
    {
        private int operationsCalls;

        public required Func<int, HttpResponseMessage> Operations { get; init; }

        public int ReleaseDeploymentAfterOperationsCalls { get; init; }

        public string DeploymentState { get; init; } = "Succeeded";

        public int OperationsCalls => Volatile.Read(ref operationsCalls);

        public ConcurrentQueue<string> OperationsRequests { get; } = new();

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                return Status(HttpStatusCode.OK, """{"properties":{"provisioningState":"Succeeded"}}""");
            }
            if (request.Method == HttpMethod.Put)
            {
                return Status(HttpStatusCode.Created, """{"name":"dep","properties":{"provisioningState":"Running"}}""");
            }
            if (path.EndsWith("/operations", StringComparison.Ordinal) || path == "/next-page")
            {
                OperationsRequests.Enqueue(request.RequestUri.AbsoluteUri);
                return Operations(Interlocked.Increment(ref operationsCalls));
            }

            SpinWait.SpinUntil(() => OperationsCalls >= ReleaseDeploymentAfterOperationsCalls, TimeSpan.FromSeconds(30));
            return Status(HttpStatusCode.OK, $$$"""{"name":"dep","properties":{"provisioningState":"{{{DeploymentState}}}"}}""");
        }
    }

    private sealed class CapturingLogger : ILoggingService
    {
        private readonly ConcurrentQueue<string> verbose = new();

        public IReadOnlyList<string> VerboseLines => [.. verbose];

        public IReadOnlyList<string> ResourceLines => [.. verbose.Where(line => line.StartsWith("Resource ", StringComparison.Ordinal))];

        public void Verbose(string message) => verbose.Enqueue(message);

        public void Log(string message) { }

        public void Info(string message) { }

        public void Debug(string message) { }

        public void Warn(string message) { }

        public void Error(string message) { }
    }
}
