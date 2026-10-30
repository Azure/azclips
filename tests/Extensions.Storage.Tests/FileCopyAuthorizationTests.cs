// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Web;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class FileCopyAuthorizationTests
{
    private const string Subscription = "55555555-5555-5555-5555-555555555555";
    private const string Sas = "sv=2026-06-06&sp=r&sr=f&sig=private-signature";
    private static string Key => Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
    private static StorageContext Source => new("sourceaccount", new Profile
    {
        Name = "source", TenantId = BlobCopyTests.Tenant, SubscriptionId = Subscription, CloudName = "AzureCloud"
    });

    [Fact]
    public void ServiceSasMatchesIndependentNodeVectorAndPreservesSnapshot()
    {
        var context = Source;
        var resource = new Uri(context.GetFileUri("share", "dir/\u96ea+%#").AbsoluteUri + "?sharesnapshot=snapshot%2B%2F%3D%26");
        var start = DateTimeOffset.Parse("2026-10-22T00:00:00Z");
        var signed = StorageCopyAuthorization.SignFileServiceSas(resource, context, Key, start, start.AddHours(1));
        var query = HttpUtility.ParseQueryString(signed.Query);
        Assert.Equal("tMBZdCiCnQ+Bza4KkgZoXY918FMgJ484gGc+K55jMLM=", query["sig"]);
        Assert.Equal("r", query["sp"]);
        Assert.Equal("https", query["spr"]);
        Assert.Equal("f", query["sr"]);
        Assert.Equal("snapshot+/=&", query["sharesnapshot"]);
        Assert.Null(query["skoid"]); Assert.Null(query["sks"]); Assert.Null(query["si"]);
    }

    [Fact]
    public async Task SuppliedKeySignsWithoutSourceAuthenticationOrArmLookup()
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync);
        var command = Command(fixture);
        command.SourceAccountKey = Key;
        await command.ProcessAsync();
        Assert.Equal(1, fixture.Sends);
        Assert.Same(fixture.Profile, Assert.Single(fixture.TokenRequests).Profile);
        var query = HttpUtility.ParseQueryString(service.AuthorizedSource!.Query);
        Assert.Equal("r", query["sp"]); Assert.Null(query["sks"]);
        Assert.Equal(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(5), DateTimeOffset.Parse(query["se"]!) - DateTimeOffset.Parse(query["st"]!));
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(fixture.Output));
        Assert.DoesNotContain(query["sig"]!, JsonSerializer.Serialize(fixture.Output));
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains(Key, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitLookupUsesSourceArmProfileAndRequestedKey(bool explicitSubscription)
    {
        const string other = "66666666-6666-6666-6666-666666666666";
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(request =>
        {
            if (request.RequestUri!.IdnHost != "management.azure.com") return service.SendAsync(request);
            Assert.Equal($"/subscriptions/{(explicitSubscription ? other : Subscription)}/resourceGroups/source-rg/providers/Microsoft.Storage/storageAccounts/sourceaccount/listKeys",
                request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    keys = new[] { new { keyName = "key2", value = Key, permissions = "Full" } }
                }))
            });
        });
        var command = Command(fixture);
        command.UseSourceAccountKeyLookup = true; command.SourceKeyResourceGroup = "source-rg"; command.SourceKeyName = "KEY2";
        command.SourceKeySubscriptionId = explicitSubscription ? other : null;
        await command.ProcessAsync();
        Assert.Equal(2, fixture.Sends);
        Assert.Same(command.SourceContext!.Profile, fixture.TokenRequests.First().Profile);
        Assert.Equal(new[] { "https://management.azure.com/.default" }, fixture.TokenRequests.First().Scopes);
        Assert.Same(fixture.Profile, fixture.TokenRequests.Last().Profile);
        Assert.Single(fixture.Output);
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(fixture.Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedSasWinsAndFailureNeverFallsBackToKeys(bool failure)
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(request => failure
            ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthenticationFailed"))
            : service.SendAsync(request));
        var command = Command(fixture);
        command.SourceSas = Sas;
        command.SourceAccountKey = "private-invalid-key";
        command.UseSourceAccountKeyLookup = true;
        command.SourceKeyResourceGroup = "invalid/group";
        command.SourceKeySubscriptionId = "invalid-subscription";
        if (failure)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => command.ProcessAsync());
            Assert.Empty(fixture.Output);
        }
        else
        {
            await command.ProcessAsync();
            Assert.Equal("private-signature", HttpUtility.ParseQueryString(service.AuthorizedSource!.Query)["sig"]);
        }
        Assert.Equal(1, fixture.Sends);
        Assert.Same(fixture.Profile, Assert.Single(fixture.TokenRequests).Profile);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(fixture.Output));
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains("private", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("no-authorization")]
    [InlineData("empty-key")]
    [InlineData("invalid-key")]
    [InlineData("conflicting-modes")]
    [InlineData("missing-group")]
    [InlineData("invalid-subscription")]
    [InlineData("unused-lookup-settings")]
    [InlineData("delegation-sas")]
    [InlineData("invalid-sas")]
    [InlineData("selector-only-sas")]
    [InlineData("blob-key")]
    public async Task InvalidAuthorizationFailsBeforeAnyCredentialsOrRequests(string damage)
    {
        var fixture = new Fixture();
        var command = Command(fixture);
        switch (damage)
        {
            case "empty-key": command.SourceAccountKey = ""; break;
            case "invalid-key": command.SourceAccountKey = "private-invalid-key"; break;
            case "conflicting-modes": command.SourceAccountKey = Key; command.UseSourceAccountKeyLookup = true; break;
            case "missing-group": command.UseSourceAccountKeyLookup = true; break;
            case "invalid-subscription":
                command.UseSourceAccountKeyLookup = true; command.SourceKeyResourceGroup = "rg"; command.SourceKeySubscriptionId = "private-invalid"; break;
            case "unused-lookup-settings": command.SourceAccountKey = Key; command.SourceKeyResourceGroup = "rg"; break;
            case "delegation-sas": command.SourceSas = Sas + "&sks=f"; break;
            case "invalid-sas": command.SourceSas = "sv=2026-06-06&sp=w&sig=private"; command.UseSourceAccountKeyLookup = true; break;
            case "selector-only-sas": command.SourceSas = "sharesnapshot=chosen"; command.SourceAccountKey = Key; break;
            case "blob-key":
                command.SourceShareName = null; command.SourcePath = null;
                command.SourceContainer = "container"; command.SourceBlob = "blob"; command.SourceAccountKey = Key; break;
        }
        var error = await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.DoesNotContain("private", error.ToString());
        Assert.Empty(fixture.TokenRequests); Assert.Equal(0, fixture.Sends); Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task MissingNamedKeyStopsBeforeCopyWithoutReturningKeys()
    {
        var service = new BlobCopyTests.CopyService();
        var fixture = new Fixture(service.SendAsync);
        var command = Command(fixture);
        command.UseSourceAccountKeyLookup = true; command.SourceKeyResourceGroup = "source-rg"; command.SourceKeyName = "key2";
        await Assert.ThrowsAsync<InvalidDataException>(() => command.ProcessAsync());
        Assert.Equal(1, fixture.Sends); Assert.Empty(fixture.Output);
    }

    private static StartStorageBlobCopyProcessor Command(Fixture fixture)
    {
        var command = new StartStorageBlobCopyProcessor
        {
            Context = BlobWorkflowTests.Context(), Container = "dest", Blob = "target",
            SourceContext = Source, SourceShareName = "share", SourcePath = "file"
        };
        command.Init(fixture.Context);
        return command;
    }
}
