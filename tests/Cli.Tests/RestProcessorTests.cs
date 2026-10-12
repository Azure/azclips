// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline, deterministic tests for the security-sensitive URL resolution / validation logic in
/// <see cref="RestProcessor_Orchestrator"/>. Exercises the processor through its public <see cref="RestProcessor_Orchestrator.ProcessAsync"/>
/// path using hand-written fakes for the runtime context (pipeline, publisher, auth, output),
/// matching the stub-based style used elsewhere in Cli.Tests (no mocking framework).
/// </summary>
public class RestProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static RestProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        string subscriptionId = TestSubscriptionId,
        FakeEventPublisher? publisher = null)
    {
        var processor = new RestProcessor_Orchestrator();
        var profile = new Profile() { TenantId = "test-tenant", SubscriptionId = subscriptionId };
        var context = new ProcessorContext(
            pipeline,
            publisher ?? new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        return processor;
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonHttpsAbsoluteUrl_BeforeSending()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "http://management.azure.com/subscriptions/x/resourcegroups?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("must use https", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonArmHost_BeforeSending()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "https://example.com/subscriptions/x/resourcegroups?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("not an allowed Azure Resource Manager endpoint", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_PrefixesRelativePath_WithArmEndpoint_AndSubstitutesSubscriptionId()
    {
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.NotNull(sentUri);
        Assert.Equal("https", sentUri!.Scheme);
        Assert.Equal("management.azure.com", sentUri.Host);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups?api-version=2021-04-01",
            sentUri.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WritesOutput_ThenThrows_OnHttpError()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found",
            Content = new StringContent("{\"error\":\"missing\"}", Encoding.UTF8, "application/json")
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups/does-not-exist?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(1, pipeline.SendCount);

        // The full response payload must be surfaced to output BEFORE the failure is thrown.
        var written = Assert.Single(output.WrittenValues);
        var response = Assert.IsType<RestResponse>(written);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("missing", response.Content);
    }

    // ----- ByParameters mode (Feature B) -------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_ByParameters_BuildsArmUrl_AndSubstitutesAmbientSubscription()
    {
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "azclips-test-group-1";
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["storageAccounts"];
        processor.Name = ["myaccount"];
        processor.ApiVersion = "2023-01-01";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.NotNull(sentUri);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/azclips-test-group-1/providers/Microsoft.Storage/storageAccounts/myaccount?api-version=2023-01-01",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_OneFewerName_ProducesCollectionGet()
    {
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "azclips-test-group-1";
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["storageAccounts"];
        // No Name → collection GET on the last type.
        processor.ApiVersion = "2023-01-01";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/azclips-test-group-1/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_InterleavesMultipleTypeAndNameSegments()
    {
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "rg1";
        processor.ResourceProviderName = "Microsoft.Network";
        processor.ResourceType = ["virtualNetworks", "subnets"];
        processor.Name = ["vnet1", "subnet1"];
        processor.ApiVersion = "2023-05-01";

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/rg1/providers/Microsoft.Network/virtualNetworks/vnet1/subnets/subnet1?api-version=2023-05-01",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_MissingApiVersion_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Resources";
        processor.ResourceType = ["resourceGroups"];
        // ApiVersion omitted.

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("ApiVersion is required", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_TooManyNameSegments_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["storageAccounts"];
        processor.Name = ["a", "b"];
        processor.ApiVersion = "2023-01-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("must equal ResourceType", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_GapOfTwoTypes_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["a", "b"];
        // Name omitted → a gap of two types with no interleaved name (malformed /a/b path).
        processor.ApiVersion = "2023-01-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("must equal ResourceType", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public async Task ProcessAsync_ByParameters_EmptyResourceTypeSegment_Throws(string resourceType)
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = [resourceType];
        processor.Name = ["myaccount"];
        processor.ApiVersion = "2023-01-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("ResourceType segments must be non-empty", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public async Task ProcessAsync_ByParameters_EmptyNameSegment_Throws(string name)
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["storageAccounts"];
        processor.Name = [name];
        processor.ApiVersion = "2023-01-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("Name segments must be non-empty", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_SuppliesBothUriAndByParameters_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01";
        processor.ResourceProviderName = "Microsoft.Resources";
        processor.ApiVersion = "2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("not both", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_NeitherUriNorByParameters_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("endpoint is required", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_NoSubscriptionAvailable_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output, subscriptionId: "");
        processor.ResourceProviderName = "Microsoft.Resources";
        processor.ResourceType = ["resourceGroups"];
        processor.ApiVersion = "2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("No subscription is available", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_NormalInput_ProducesUnchangedUrl_RegressionGuard()
    {
        // Regression guard: ordinary id segments contain only unreserved characters, so
        // Uri.EscapeDataString must leave them byte-for-byte identical to the pre-encoding URL.
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["storageAccounts"];
        // No Name → collection GET.
        processor.ApiVersion = "2023-01-01";

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_ByParameters_SegmentWithReservedChars_IsPercentEncoded()
    {
        // A resource group name containing reserved/structural characters must be percent-encoded so
        // it cannot inject extra path ('/') or query ('?') structure. The structural separators the
        // builder itself emits stay intact.
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "bad/name?x";
        processor.ResourceProviderName = "Microsoft.Storage";
        processor.ResourceType = ["storageAccounts"];
        processor.ApiVersion = "2023-01-01";

        await processor.ProcessAsync();

        string url = sentUri!.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped);

        // Reserved characters in the value are encoded — no extra '/' or '?' structure injected.
        Assert.Contains("/resourceGroups/bad%2Fname%3Fx/", url);
        Assert.DoesNotContain("/resourceGroups/bad/name", url);
        // Structural separators and the rest of the path/query are unchanged.
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/bad%2Fname%3Fx/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01",
            url);
    }

    [Fact]
    public async Task ProcessAsync_NonJsonResponse_DoesNotPublishInvalidJson_AndSurfacesRawBody()
    {
        var publisher = new FakeEventPublisher();
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("plain text", Encoding.UTF8, "text/plain")
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output, publisher: publisher);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        // A non-JSON passthrough body must NOT produce ResponseInvalidJson noise.
        Assert.DoesNotContain(Events.ResponseInvalidJson, publisher.PublishedIds);

        var written = Assert.Single(output.WrittenValues);
        var response = Assert.IsType<RestResponse>(written);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("plain text", response.Content);
        Assert.Null(response.Json);
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonArmHost_ListsAllowedHostsInDeterministicOrder()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "https://example.com/subscriptions/x/resourcegroups?api-version=2021-04-01";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains(
            "Allowed hosts: management.azure.com, management.chinacloudapi.cn, management.microsoftazure.de, management.usgovcloudapi.net.",
            ex.Message);
    }

    // ----- UriParameters (Feature C) -----------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_UriParameters_AppendedToUrlWithNoExistingQuery()
    {
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups";
        processor.UriParameters = ["$top=3"];

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups?%24top=3",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_UriParameters_AppendedWithAmpersand_WhenQueryAlreadyPresent()
    {
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01";
        processor.UriParameters = ["$top=3", "$filter=name eq 'x'"];

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups?api-version=2021-04-01&%24top=3&%24filter=name%20eq%20%27x%27",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ProcessAsync_UriParameters_AppendedToByParametersUrl()
    {
        Uri? sentUri = null;
        var pipeline = CapturingPipeline(uri => sentUri = uri);
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.ResourceProviderName = "Microsoft.Resources";
        processor.ResourceType = ["resourceGroups"];
        processor.ApiVersion = "2021-04-01";
        processor.UriParameters = ["$top=3"];

        await processor.ProcessAsync();

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/providers/Microsoft.Resources/resourceGroups?api-version=2021-04-01&%24top=3",
            sentUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_UriParameters_InvalidEntryWithoutEquals_Throws()
    {
        var pipeline = CapturingPipeline(_ => { });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);
        processor.Uri = "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01";
        processor.UriParameters = ["notakeyvalue"];

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Contains("Expected KEY=VALUE", ex.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    private static FakeHttpPipeline CapturingPipeline(Action<Uri?> capture)
    {
        return new FakeHttpPipeline((request, _) =>
        {
            capture(request.RequestUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
    }

    // ----- Fakes -------------------------------------------------------------------------------
    // The runtime-context fakes (FakeOutputHandler, FakeAuthHandler, FakeHttpPipeline,
    // FakeEventPublisher) are shared across processor tests in tests/Cli.Tests/Fakes/ProcessorFakes.cs.
}


