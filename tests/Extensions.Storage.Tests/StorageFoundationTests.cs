// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Xml.Linq;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime;
using Core.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Moq;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;
using FileModels = Microsoft.Azure.Azclips.Extensions.Storage.Files.Models;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;
using ManagementProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Management.Processors;

namespace Extensions.Storage.Tests;

public class StorageFoundationTests
{
    private const string BlobUrl = "https://fixture.blob.core.windows.net/container/blob";
    private const string FileUrl = "https://fixture.file.core.windows.net/share/directory/file";

    [Fact]
    public async Task ContextConstructionIsLocalAndContainsNoAccessToken()
    {
        var fixture = new Fixture();
        var processor = new NewStorageContextProcessor
        {
            StorageAccountName = "fixture", EnableFileBackupRequestIntent = true
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var context = Assert.IsType<StorageContext>(Assert.Single(fixture.Output));
        Assert.Equal("https://fixture.blob.core.windows.net/", context.BlobEndpoint.AbsoluteUri);
        Assert.Equal("https://fixture.file.core.windows.net/", context.FileEndpoint.AbsoluteUri);
        Assert.True(context.EnableFileBackupRequestIntent);
        Assert.Same(fixture.Profile, context.Profile);
        Assert.DoesNotContain("AccessToken", System.Text.Json.JsonSerializer.Serialize(context));
        Assert.Empty(fixture.TokenRequests);
        Assert.Equal(0, fixture.Sends);
    }

    [Fact]
    public async Task ContextDoesNotSilentlyChangeAuthenticationModes()
    {
        var fixture = new Fixture();
        var processor = new NewStorageContextProcessor { StorageAccountName = "fixture", UseConnectedAccount = false };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<NotSupportedException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.Empty(fixture.TokenRequests);
        Assert.Equal(0, fixture.Sends);
    }

    [Theory]
    [InlineData("http://fixture.blob.core.windows.net/container/blob")]
    [InlineData("https://fixture.blob.core.windows.net.attacker.example/container/blob")]
    [InlineData("https://other.fixture.blob.core.windows.net/container/blob")]
    [InlineData("https://fixture.blob.core.windows.net:444/container/blob")]
    [InlineData("https://user@fixture.blob.core.windows.net/container/blob")]
    [InlineData("https://fixture.blob.core.windows.net/container/blob#fragment")]
    [InlineData("https://fixture.blob.core.usgovcloudapi.net/container/blob")]
    [InlineData("https://fixture.file.core.windows.net/share/file")]
    public async Task InvalidEndpointFailsBeforeAuthentication(string url)
    {
        var fixture = new Fixture();
        var processor = new ProbeProcessor { Url = url };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Equal(0, fixture.Sends);
    }

    [Theory]
    [InlineData("https://other.blob.core.windows.net/container/blob")]
    [InlineData("https://fixture.blob.core.windows.net/container/other")]
    [InlineData("https://fixture.blob.core.windows.net/container/blob/")]
    [InlineData("https://fixture.blob.core.windows.net/container/blob#fragment")]
    public async Task RewrittenRequestCannotChangeTheSelectedResource(string requestUrl)
    {
        var fixture = new Fixture();
        var processor = new ProbeProcessor { Url = BlobUrl, RequestUrl = requestUrl };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Equal(0, fixture.Sends);
    }

    [Theory]
    [InlineData("?sig=secret")]
    [InlineData("?SIG=secret")]
    [InlineData("?sv=2026-06-06&sp=r")]
    public async Task DestinationSasIsRejected(string query)
    {
        var fixture = new Fixture();
        var processor = new ProbeProcessor { Url = BlobUrl + query };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<NotSupportedException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task TenantIsRequiredBeforeTokenAcquisition(string? tenant)
    {
        var fixture = new Fixture(profile: new Profile { TenantId = tenant! });
        var processor = new ProbeProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task SovereignCloudIsNotSilentlyTreatedAsPublic()
    {
        var fixture = new Fixture(profile: new Profile { TenantId = "tenant", CloudName = "AzureUSGovernment" });
        var processor = new ProbeProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<NotSupportedException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task FilesRequireExplicitIntent()
    {
        var fixture = new Fixture();
        var processor = new FileProcessors.GetPropertiesFileProcessor { Url = FileUrl, FileRequestIntent = "backup" };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task FilesIntentAndSourceProfileFlowThroughCrossServiceChildren()
    {
        var sourceProfile = new Profile { Name = "source", TenantId = "source-tenant" };
        var fixture = new Fixture(request =>
        {
            Assert.Equal(FileUrl, request.RequestUri!.AbsoluteUri);
            Assert.Equal("backup", Assert.Single(request.Headers.GetValues("x-ms-file-request-intent")));
            return Task.FromResult(Response());
        });
        var parent = new ProbeProcessor
        {
            Url = BlobUrl, Context = new StorageContext("fixture", sourceProfile, enableFileBackupRequestIntent: true)
        };
        parent.Init(fixture.Context);
        var child = parent.CreateFileChild(FileUrl);
        await child.ProcessAsync();
        var token = Assert.Single(fixture.TokenRequests);
        Assert.Same(sourceProfile, token.Profile);
        Assert.Equal(["https://storage.azure.com/.default"], token.Scopes);
        Assert.Same(fixture.Profile, fixture.Auth.Profile);
    }

    [Fact]
    public async Task ContextCannotAddressAnotherAccount()
    {
        var fixture = new Fixture();
        var processor = new ProbeProcessor { Url = BlobUrl, Context = new StorageContext("other", fixture.Profile) };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task SnapshotQueryIsPreservedWithoutChangingTheName()
    {
        const string url = "https://fixture.blob.core.windows.net/container/a%20b%23c%25/";
        var fixture = new Fixture(request =>
        {
            Assert.Equal(new Uri(url).AbsolutePath, request.RequestUri!.AbsolutePath);
            Assert.Equal("version with+characters", System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["versionid"]);
            return Task.FromResult(Response());
        });
        var processor = new BlobProcessors.GetPropertiesBlobProcessor
        {
            Url = url + "?versionid=" + Uri.EscapeDataString("version with+characters")
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task ConflictingVersionSelectorsFailBeforeAuthentication()
    {
        var fixture = new Fixture();
        var processor = new BlobProcessors.GetPropertiesBlobProcessor { Url = BlobUrl + "?versionid=one", VersionId = "two" };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task UploadUsesRawBytesCorrectContentHeadersAndPerKeyMetadata()
    {
        byte[] payload = [0, 255, 13, 10, 128, 1, 2, 3];
        byte[] digest = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        var fixture = new Fixture(async request =>
        {
            Assert.Equal(BlobUrl, request.RequestUri!.AbsoluteUri);
            Assert.Equal(payload, await request.Content!.ReadAsByteArrayAsync());
            Assert.Equal(payload.Length, request.Content.Headers.ContentLength);
            Assert.Equal(digest, request.Content.Headers.ContentMD5);
            Assert.Equal("value", Assert.Single(request.Headers.GetValues("x-ms-meta-name")));
            Assert.False(request.Headers.Contains("x-ms-meta"));
            Assert.True(request.HasSensitiveDiagnostics());
            return Response(HttpStatusCode.Created);
        });
        var processor = new BlobProcessors.UploadBlockBlobProcessor
        {
            Url = BlobUrl, Body = payload, ContentLength = payload.Length, TransactionalContentMd5 = digest,
            BlobType = "BlockBlob", Metadata = new Dictionary<string, string> { ["name"] = "value" }
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(["https://storage.azure.com/.default"], Assert.Single(fixture.TokenRequests).Scopes);
    }

    [Fact]
    public async Task DeclaredBinaryLengthMustMatchActualBytes()
    {
        var fixture = new Fixture();
        var processor = new BlobProcessors.UploadBlockBlobProcessor { Url = BlobUrl, Body = [1, 2], ContentLength = 3 };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task OversizedUploadIsRejectedBeforeAuthentication()
    {
        var fixture = new Fixture();
        byte[] data = new byte[StorageWireFormat.MaximumChunkBytes + 1];
        var processor = new BlobProcessors.UploadBlockBlobProcessor { Url = BlobUrl, Body = data, ContentLength = data.Length };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Equal(0, fixture.Sends);
    }

    [Theory]
    [InlineData("invalid-key", "value")]
    [InlineData("name", "value\r\ninjected-header: value")]
    public async Task InvalidMetadataFailsBeforeAuthentication(string key, string value)
    {
        var fixture = new Fixture();
        var processor = new BlobProcessors.UploadBlockBlobProcessor
        {
            Url = BlobUrl, Body = [], ContentLength = 0,
            Metadata = new Dictionary<string, string> { [key] = value }
        };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task FilesDoNotSilentlyReplaceAnInvalidIntent()
    {
        var fixture = new Fixture();
        var processor = new FileProcessors.GetPropertiesFileProcessor
        {
            Url = FileUrl, FileRequestIntent = "unsupported", Context = new StorageContext("fixture", fixture.Profile, true)
        };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task RetriesPreserveBinaryBodyAndRequestScopedAuthentication()
    {
        byte[] payload = [0, 1, 255, 42];
        int attempts = 0;
        var fixture = new Fixture(async request =>
        {
            Assert.Equal(payload, await request.Content!.ReadAsByteArrayAsync());
            Assert.True(request.HasSensitiveDiagnostics());
            Assert.True(request.Options.TryGetValue(RequestAuthentication.OptionsKey, out var authentication));
            Assert.Equal(["https://storage.azure.com/.default"], authentication.Scopes);
            return Response(++attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Created);
        });
        var processor = new BlobProcessors.StageBlockBlockBlobProcessor
        {
            Url = BlobUrl, Body = payload, ContentLength = payload.Length, BlockId = [1, 2]
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
        Assert.Equal(["https://storage.azure.com/.default"], Assert.Single(fixture.TokenRequests).Scopes);
    }

    [Fact]
    public async Task CommitBlockListIsXmlWithBase64Ids()
    {
        var fixture = new Fixture(async request =>
        {
            Assert.Equal("application/xml", request.Content!.Headers.ContentType!.MediaType);
            var xml = XElement.Parse(await request.Content.ReadAsStringAsync());
            Assert.Equal("BlockList", xml.Name.LocalName);
            Assert.Equal(new[] { "Committed", "Uncommitted", "Latest" }, xml.Elements().Select(element => element.Name.LocalName));
            Assert.Equal(new[] { "AQI=", "AwQ=", "BQY=" }, xml.Elements().Select(element => element.Value));
            return Response(HttpStatusCode.Created);
        });
        var processor = new BlobProcessors.CommitBlockListBlockBlobProcessor
        {
            Url = BlobUrl, Committed = [[1, 2]], Uncommitted = [[3, 4]], Latest = [[5, 6]]
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
    }

    [Fact]
    public async Task HeaderOnlyResponseRetainsLargeLengthAndMetadata()
    {
        long size = 5L * 1024 * 1024 * 1024;
        var fixture = new Fixture(_ =>
        {
            var response = Response();
            response.Headers.ETag = new EntityTagHeaderValue("\"fixture-etag\"");
            response.Headers.Add("x-ms-meta-owner", "fixture");
            response.Content.Headers.ContentLength = size;
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "utf-8" };
            return Task.FromResult(response);
        });
        var processor = new BlobProcessors.GetPropertiesBlobProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var properties = Assert.IsType<StorageResourceProperties>(Assert.Single(fixture.Output));
        Assert.Equal(size, properties.ContentLength);
        Assert.Equal("\"fixture-etag\"", properties.ETag);
        Assert.Equal("text/plain; charset=utf-8", properties.ContentType);
        Assert.Equal("fixture", properties.Metadata["owner"]);
    }

    [Fact]
    public async Task BinaryDownloadIsNotDecodedAsTextOrJson()
    {
        byte[] payload = [0, 255, 129, 13, 10, 42];
        var fixture = new Fixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }));
        var processor = new BlobProcessors.DownloadBlobProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(payload, Assert.IsType<byte[]>(Assert.Single(fixture.Output)));
    }

    [Fact]
    public async Task OversizedBufferedDownloadFailsInsteadOfBufferingWholeFile()
    {
        using var content = new StreamContent(new NonSeekableStream(new byte[StorageWireFormat.MaximumChunkBytes + 1]));
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => StorageWireFormat.ReadChunkAsync(content, CancellationToken.None));
        Assert.Contains("streaming", exception.Message);
    }

    [Fact]
    public async Task StreamingDownloadCanExceedTheBufferedChunkLimit()
    {
        byte[] data = new byte[StorageWireFormat.MaximumChunkBytes + 17];
        Random.Shared.NextBytes(data);
        using var content = new StreamContent(new NonSeekableStream(data));
        using var output = new MemoryStream();
        await StorageWireFormat.CopyToAsync(content, output, CancellationToken.None);
        Assert.Equal(SHA256.HashData(data), SHA256.HashData(output.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelegationRequestAndResponseUseXml(bool files)
    {
        const string reply = "<UserDelegationKey><SignedOid>oid</SignedOid><SignedTid>tid</SignedTid><SignedStart>start</SignedStart><SignedExpiry>expiry</SignedExpiry><SignedService>b</SignedService><SignedVersion>2026-06-06</SignedVersion><Value>c2VjcmV0</Value></UserDelegationKey>";
        var fixture = new Fixture(async request =>
        {
            Assert.Equal("/", request.RequestUri!.AbsolutePath);
            var xml = XElement.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("KeyInfo", xml.Name.LocalName);
            Assert.Equal("2026-10-21T00:00:00Z", (string?)xml.Element("Expiry"));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply) };
        });
        StorageProcessorBase processor = files
            ? new FileProcessors.GetUserDelegationKeyServiceProcessor
            {
                Url = "https://fixture.file.core.windows.net/", Expiry = "2026-10-21T00:00:00Z",
                Context = new StorageContext("fixture", fixture.Profile, true)
            }
            : new BlobProcessors.GetUserDelegationKeyServiceProcessor
            {
                Url = "https://fixture.blob.core.windows.net/", Expiry = "2026-10-21T00:00:00Z"
            };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        object result = Assert.Single(fixture.Output);
        Assert.Equal("c2VjcmV0", files ? Assert.IsType<FileModels.UserDelegationKey>(result).Value : Convert.ToBase64String(Assert.IsType<BlobModels.UserDelegationKey>(result).Value));
    }

    [Theory]
    [InlineData("<!DOCTYPE Tags [<!ENTITY fixture SYSTEM 'file:///private-fixture'>]><Tags><TagSet>&fixture;</TagSet></Tags>")]
    [InlineData("<Tags><TagSet><Tag><Key>private-fixture</Key></Tag></TagSet></Tags>")]
    [InlineData("<Tags>private-fixture")]
    public async Task InvalidXmlDoesNotLeakPayload(string payload)
    {
        var fixture = new Fixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) }));
        var processor = new BlobProcessors.GetTagsBlobProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-fixture", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task DeniedRequestRemainsAnErrorWithoutLeakingBodyOrSubstitutedRequest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("private-fixture"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://other.example/private-fixture")
        };
        var fixture = new Fixture(_ => Task.FromResult(response));
        var processor = new ProbeProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.DoesNotContain("private-fixture", error.ToString());
        Assert.True(response.RequestMessage.HasSensitiveDiagnostics());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task CancellationFlowsFromThePublisherAndStopsBeforeAuth()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(token: cancellation.Token, cancel: cancellation.Cancel);
        var processor = new ProbeProcessor { Url = BlobUrl };
        processor.Init(fixture.Context);
        Assert.Equal(cancellation.Token, processor.Token);
        processor.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Contains(Events.ProcessorProcessAsyncEnd, fixture.Events);
    }

    [Fact]
    public async Task ArmKeysUseArmAudienceNotStorageAudience()
    {
        var fixture = new Fixture(request =>
        {
            Assert.EndsWith("/listKeys", request.RequestUri!.AbsolutePath);
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            Assert.Equal("2025-08-01", query["api-version"]);
            Assert.Equal("kerb", query["$expand"]);
            Assert.Equal(2, query.Count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"keys\":[]}") });
        });
        var processor = new ManagementProcessors.ListKeysStorageAccountsProcessor
        {
            SubscriptionId = "subscription", ResourceGroupName = "group", AccountName = "fixture", Expand = "kerb"
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        Assert.Equal(["https://management.azure.com/.default"], Assert.Single(fixture.TokenRequests).Scopes);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{private-fixture")]
    public async Task InvalidArmPayloadDoesNotBecomeEmptySuccessOrLeakContent(string payload)
    {
        var fixture = new Fixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) }));
        var processor = new ManagementProcessors.ListKeysStorageAccountsProcessor
        {
            SubscriptionId = "subscription", ResourceGroupName = "group", AccountName = "fixture"
        };
        processor.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.DoesNotContain("private-fixture", error.ToString());
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public void NativeNamesPreserveEscapingAndTrailingSlash()
    {
        var context = new StorageContext("fixture", Fixture.DefaultProfile);
        var uri = context.GetBlobUri("container", "dir/a b#%/");
        Assert.Equal("/container/dir/a%20b%23%25/", uri.AbsolutePath);
        Assert.Equal("/share/dir/file.", context.GetFileUri("share", "dir/file.").AbsolutePath);
        Assert.Throws<ArgumentException>(() => context.GetBlobUri("container", "../other"));
    }

    private static HttpResponseMessage Response(HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new ByteArrayContent([]) };

    private class ProbeProcessor : BlobProcessors.BlobProcessorBase<object, object>
    {
        public string? RequestUrl { get; set; }
        protected override HttpMethod Method => HttpMethod.Head;
        protected override string CreateRequestUri() => RequestUrl ?? Endpoint;
        public FileProcessors.GetPropertiesFileProcessor CreateFileChild(string url) =>
            CreateExtendedProcessor<FileProcessors.GetPropertiesFileProcessor>(processor => processor.Url = url);
    }

    internal sealed class Fixture
    {
        internal static readonly Profile DefaultProfile = new() { Name = "destination", TenantId = "tenant", CloudName = "AzureCloud" };
        internal Profile Profile { get; }
        internal AuthHandler Auth { get; }
        internal ProcessorContext Context { get; }
        internal ConcurrentQueue<TokenRequest> TokenRequests { get; } = new();
        internal ConcurrentQueue<object> Output { get; } = new();
        internal ConcurrentQueue<string> Events { get; } = new();
        internal ConcurrentQueue<(string Id, string Message)> Messages { get; } = new();
        internal ConcurrentQueue<EventData> Progress { get; } = new();
        private int sends;
        internal int Sends => sends;

        internal Fixture(Func<HttpRequestMessage, Task<HttpResponseMessage>>? send = null, Profile? profile = null,
            CancellationToken token = default, Action? cancel = null)
        {
            Profile = profile ?? DefaultProfile;
            var publisher = new Mock<IEventPublisher>();
            publisher.SetupGet(value => value.Token).Returns(token);
            publisher.SetupGet(value => value.Cancel).Returns(cancel ?? (() => { }));
            publisher.Setup(value => value.Publish(It.IsAny<string>()))
                .Callback<string>(id => Events.Enqueue(id)).Returns(Task.CompletedTask);
            publisher.Setup(value => value.Publish(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((id, message) => Messages.Enqueue((id, message))).Returns(Task.CompletedTask);
            publisher.Setup(value => value.Publish(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<EventData>()))
                .Callback<string, CancellationToken, EventData>((id, _, data) =>
                {
                    if (id == Core.Abstractions.Runtime.Events.Progress) Progress.Enqueue(data);
                }).Returns(Task.CompletedTask);
            Auth = new AuthHandler
            {
                Profile = Profile,
                AcquireTokenAsyncDefault = (scopes, _, _, cancellationToken, selectedProfile) =>
                {
                    TokenRequests.Enqueue(new TokenRequest(scopes!, selectedProfile!, cancellationToken));
                    return Task.FromResult(new AuthResult
                    {
                        AccessToken = "fixture-token", TenantId = selectedProfile!.TenantId,
                        ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5), Scopes = scopes ?? []
                    });
                }
            };
            var terminal = new Terminal(async request =>
            {
                Interlocked.Increment(ref sends);
                return send is null ? Response() : await send(request);
            });
            var pipeline = new HttpPipeline(terminal, maxRetry: 1, maxRetryFor429: 1);
            pipeline.Prepend(Auth.SendAsync);
            var output = new Mock<IOutputHandler>();
            output.SetupGet(value => value.WriteOutput).Returns((object value, CancellationToken _) =>
            {
                Output.Enqueue(value);
                return Task.CompletedTask;
            });
            Context = new ProcessorContext(pipeline, publisher.Object, Auth, output.Object, Profile);
        }
    }

    internal sealed record TokenRequest(string[] Scopes, Profile Profile, CancellationToken CancellationToken);

    private sealed class Terminal(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : ISendAsync, ISendAsyncTerminalFactory
    {
        public ISendAsync Create() => this;
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) => send(request);
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
