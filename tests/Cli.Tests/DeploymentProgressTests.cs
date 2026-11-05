// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Resource progress for resource group and management group deployments (#910): while the deployment runs, its operations
/// are listed and each new provisioning state is published as <see cref="Events.Progress"/>, like Az PowerShell's -Verbose.
/// </summary>
public sealed class DeploymentProgressTests
{
    private const string AsgType = "Microsoft.Network/applicationSecurityGroups";

    // Progress is published from a background task, so capture it thread-safely; Moq records calls thread-safely too.
    private readonly ConcurrentQueue<string> progress = new();
    private readonly Mock<IEventPublisher> publisher = new();

    public DeploymentProgressTests() =>
        publisher.Setup(p => p.Publish(Events.Progress, It.IsAny<string>()))
            .Callback<string, string>((_, message) => progress.Enqueue(message))
            .Returns(Task.CompletedTask);

    private IReadOnlyList<string> ProgressLines => [.. progress];

    private IReadOnlyList<string> ResourceLines => [.. progress.Where(line => line.StartsWith("Resource ", StringComparison.Ordinal))];

    [Fact]
    public async Task ResourceGroup_ReportsEachNewStateOnce_ThenCatchesUpWhenTheDeploymentFinishes()
    {
        var checks = new Checks(2);
        var arm = new FakeArm
        {
            DeploymentFinished = _ => checks.AllDone,
            Operations = call => call switch
            {
                1 => OperationsPage(Op("1", "Running", "asg-1")),
                2 => OperationsPage(Op("1", "Running", "asg-1"), Op("2", "Running", "asg-2")),
                _ => OperationsPage(Op("1", "Succeeded", "asg-1"), Op("2", "Succeeded", "asg-2")),
            },
        };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(arm, output, checks);

        await create.ProcessAsync();

        Assert.Equal(
            [
                $"Resource {AsgType} 'asg-1' provisioning status is running",
                $"Resource {AsgType} 'asg-2' provisioning status is running",
                $"Resource {AsgType} 'asg-1' provisioning status is succeeded",
                $"Resource {AsgType} 'asg-2' provisioning status is succeeded",
            ],
            ResourceLines);
        Assert.Contains("Checking deployment status in 5 seconds", ProgressLines);
        Assert.Equal(3, arm.OperationsCalls);
        Assert.All(arm.OperationsRequests, uri => Assert.Equal(DeploymentUri.Replace("/providers/Microsoft.Resources", "") + "/operations?api-version=2026-06-01", uri));
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ResourceGroup_ReadsEveryPageOfOperations()
    {
        var checks = new Checks(1);
        var arm = new FakeArm
        {
            DeploymentFinished = _ => checks.AllDone,
            Operations = call => call % 2 == 1
                ? OperationsPageWithNextLink("https://management.azure.com/next-page?skiptoken=2", Op("1", "Succeeded", "asg-1"))
                : OperationsPage(Op("2", "Succeeded", "asg-2")),
        };
        var create = ResourceGroupCreate(arm, new FakeOutputHandler(), checks);

        await create.ProcessAsync();

        Assert.Equal(
            [
                $"Resource {AsgType} 'asg-1' provisioning status is succeeded",
                $"Resource {AsgType} 'asg-2' provisioning status is succeeded",
            ],
            ResourceLines);
        Assert.Contains("https://management.azure.com/next-page?skiptoken=2", arm.OperationsRequests);
    }

    [Fact]
    public async Task ResourceGroup_SkipsFailedOperationsAndOperationsWithoutATarget_AndKeepsTheDeploymentFailure()
    {
        var checks = new Checks(1);
        var arm = new FakeArm
        {
            DeploymentFinished = _ => checks.AllDone,
            DeploymentState = "Failed",
            Operations = _ => OperationsPage(Op("1", "Succeeded", "asg-1"), Op("2", "Failed", "asg-2"), Op("3", "Running", null)),
        };
        var create = ResourceGroupCreate(arm, new FakeOutputHandler(), checks);

        Exception error = await Assert.ThrowsAnyAsync<Exception>(create.ProcessAsync);

        Assert.StartsWith("Long running operation failed", error.Message);
        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], ResourceLines);
    }

    [Fact]
    public async Task ResourceGroup_FailedOperationsRead_StopsProgressWithoutAnErrorOrChangingTheResult()
    {
        var arm = new FakeArm
        {
            DeploymentFinished = fake => fake.OperationsCalls >= 1,
            Operations = _ => Status(HttpStatusCode.Forbidden, """{"error":{"code":"AuthorizationFailed","message":"no read access"}}"""),
        };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(arm, output, new Checks(int.MaxValue));

        await create.ProcessAsync();

        Assert.Single(output.WrittenValues);
        Assert.Empty(ResourceLines);
        Assert.Contains(ProgressLines, line => line.StartsWith("Unable to read the deployment operations") && line.Contains("403"));
        // The first failure ends the checks, and no catch-up read follows it.
        Assert.Equal(1, arm.OperationsCalls);
        // A failed read must not publish ResourceOperationFailed, which PowerShell writes to the error stream.
        Assert.DoesNotContain(publisher.Invocations, call => call.Arguments.FirstOrDefault() as string == Events.ResourceOperationFailed);
    }

    [Fact]
    public async Task ResourceGroup_OperationsNotFoundYet_AreReadAgainAtTheNextCheck()
    {
        var checks = new Checks(2);
        var arm = new FakeArm
        {
            DeploymentFinished = _ => checks.AllDone,
            Operations = call => call == 1
                ? Status(HttpStatusCode.NotFound, """{"error":{"code":"DeploymentNotFound","message":"not found"}}""")
                : OperationsPage(Op("1", "Succeeded", "asg-1")),
        };
        var create = ResourceGroupCreate(arm, new FakeOutputHandler(), checks);

        await create.ProcessAsync();

        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], ResourceLines);
        Assert.DoesNotContain(ProgressLines, line => line.StartsWith("Unable to read the deployment operations"));
    }

    [Fact]
    public async Task ResourceGroup_ChecksStartOnlyAfterArmAcceptsTheDeployment()
    {
        var checks = new Checks(1);
        var arm = new FakeArm
        {
            PutDuration = TimeSpan.FromMilliseconds(500),
            DeploymentFinished = _ => checks.AllDone,
            Operations = _ => OperationsPage(Op("1", "Succeeded", "asg-1")),
        };
        var create = ResourceGroupCreate(arm, new FakeOutputHandler(), checks);

        await create.ProcessAsync();

        Assert.Equal(0, arm.OperationsCallsBeforePutReturned);
        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], ResourceLines);
    }

    [Fact]
    public async Task ResourceGroup_DeploymentFinishing_CancelsAnOperationsReadInFlight_ThenCatchesUp()
    {
        bool readStoppedByCancellation = false;
        var arm = new FakeArm
        {
            DeploymentFinished = fake => fake.OperationsCalls >= 1,
            Operations = _ => OperationsPage(Op("1", "Succeeded", "asg-1")),
            HangFirstOperationsRead = token =>
            {
                readStoppedByCancellation = SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromSeconds(30));
                throw new OperationCanceledException(token);
            },
        };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(arm, output, new Checks(int.MaxValue));

        var elapsed = Stopwatch.StartNew();
        await create.ProcessAsync();

        Assert.True(readStoppedByCancellation);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), $"The deployment waited {elapsed.Elapsed} for a progress read.");
        Assert.Single(output.WrittenValues);
        Assert.Equal(2, arm.OperationsCalls);
        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], ResourceLines);
    }

    [Fact]
    public async Task ResourceGroup_DeploymentFinishedBeforeTheFirstCheck_SendsNoOperationsRequest()
    {
        var arm = new FakeArm { Operations = _ => throw new InvalidOperationException("Operations must not be read.") };
        var output = new FakeOutputHandler();
        var create = ResourceGroupCreate(arm, output, new Checks(0));

        await create.ProcessAsync();

        Assert.Equal(0, arm.OperationsCalls);
        Assert.Empty(ResourceLines);
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task ManagementGroup_ReportsProgressFromTheManagementGroupOperations()
    {
        var checks = new Checks(1);
        var arm = new FakeArm
        {
            DeploymentFinished = _ => checks.AllDone,
            Operations = _ => OperationsPage(Op("1", "Succeeded", "asg-1")),
        };
        var output = new FakeOutputHandler();
        var create = Init(new CreateManagementGroupDeploymentProcessor_Orchestrator(), arm, output);
        create.ManagementGroupId = "mg1";
        create.Location = "eastus";
        create.Name = "dep";
        create.TemplateUri = "https://example.com/t.json";
        create.ProgressDelay = checks.Delay;

        await create.ProcessAsync();

        Assert.Equal([$"Resource {AsgType} 'asg-1' provisioning status is succeeded"], ResourceLines);
        Assert.All(arm.OperationsRequests, uri => Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/mg1/providers/Microsoft.Resources/deployments/dep/operations?api-version=2026-06-01",
            uri));
        Assert.Single(output.WrittenValues);
    }

    private NewResourceGroupDeploymentProcessor_Orchestrator ResourceGroupCreate(FakeArm arm, FakeOutputHandler output, Checks checks)
    {
        var processor = Init(new NewResourceGroupDeploymentProcessor_Orchestrator(), arm, output);
        processor.SubscriptionId = SubscriptionId;
        processor.ResourceGroupName = "test-rg";
        processor.Name = "dep";
        processor.TemplateUri = "https://example.com/t.json";
        processor.ProgressDelay = checks.Delay;
        return processor;
    }

    private T Init<T>(T processor, FakeArm arm, FakeOutputHandler output)
        where T : IProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = SubscriptionId };
        processor.Init(new ProcessorContext(
            new FakeHttpPipeline(arm.Respond),
            publisher.Object,
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }

    private static string Op(string id, string state, string? resourceName) => resourceName is null
        ? $$$"""{"operationId":"{{{id}}}","properties":{"provisioningState":"{{{state}}}"}}"""
        : $$$$"""{"operationId":"{{{{id}}}}","properties":{"provisioningState":"{{{{state}}}}","targetResource":{"resourceType":"{{{{AsgType}}}}","resourceName":"{{{{resourceName}}}}"}}}""";

    private static HttpResponseMessage OperationsPage(params string[] operations) =>
        Status(HttpStatusCode.OK, $$"""{"value":[{{string.Join(",", operations)}}]}""");

    private static HttpResponseMessage OperationsPageWithNextLink(string nextLink, params string[] operations) =>
        Status(HttpStatusCode.OK, $$"""{"value":[{{string.Join(",", operations)}}],"nextLink":"{{nextLink}}"}""");

    /// <summary>
    /// The progress checks' waits: the first <c>immediate</c> return at once, later ones last until the deployment finishes
    /// and stops them. <see cref="AllDone"/> turns true once those checks have finished reporting.
    /// </summary>
    private sealed class Checks(int immediate)
    {
        private int waits;

        public bool AllDone => Volatile.Read(ref waits) > immediate;

        public Task Delay(TimeSpan step, CancellationToken token) =>
            Interlocked.Increment(ref waits) <= immediate ? Task.CompletedTask : Task.Delay(Timeout.Infinite, token);
    }

    /// <summary>
    /// ARM for one deployment: validate succeeds, the PUT starts it, and its GETs report <see cref="DeploymentState"/> once
    /// <see cref="DeploymentFinished"/> is true, so tests decide how many progress checks happen while it runs.
    /// </summary>
    private sealed class FakeArm
    {
        private int operationsCalls;
        private volatile bool putReturned;
        private int operationsCallsBeforePutReturned;

        public required Func<int, HttpResponseMessage> Operations { get; init; }

        public Func<FakeArm, bool> DeploymentFinished { get; init; } = _ => true;

        public Action<CancellationToken>? HangFirstOperationsRead { get; init; }

        public TimeSpan PutDuration { get; init; }

        public string DeploymentState { get; init; } = "Succeeded";

        public int OperationsCalls => Volatile.Read(ref operationsCalls);

        public int OperationsCallsBeforePutReturned => Volatile.Read(ref operationsCallsBeforePutReturned);

        public ConcurrentQueue<string> OperationsRequests { get; } = new();

        public HttpResponseMessage Respond(HttpRequestMessage request, IEventPublisher callback)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                return Status(HttpStatusCode.OK, """{"properties":{"provisioningState":"Succeeded"}}""");
            }
            if (request.Method == HttpMethod.Put)
            {
                Thread.Sleep(PutDuration);
                putReturned = true;
                return Status(HttpStatusCode.Created, """{"name":"dep","properties":{"provisioningState":"Running"}}""");
            }
            if (path.EndsWith("/operations", StringComparison.Ordinal) || path == "/next-page")
            {
                OperationsRequests.Enqueue(request.RequestUri.AbsoluteUri);
                if (!putReturned)
                {
                    Interlocked.Increment(ref operationsCallsBeforePutReturned);
                }
                int call = Interlocked.Increment(ref operationsCalls);
                if (call == 1 && HangFirstOperationsRead is { } hang)
                {
                    hang(callback.Token);
                }
                return Operations(call);
            }

            SpinWait.SpinUntil(() => DeploymentFinished(this), TimeSpan.FromSeconds(30));
            return Status(HttpStatusCode.OK, $$$"""{"name":"dep","properties":{"provisioningState":"{{{DeploymentState}}}"}}""");
        }
    }
}
