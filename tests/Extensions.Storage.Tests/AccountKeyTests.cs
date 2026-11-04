// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class AccountKeyTests
{
    private const string Subscription = "11111111-1111-1111-1111-111111111111";
    private const string OtherSubscription = "22222222-2222-2222-2222-222222222222";
    private const string Secret = "c2Vuc2l0aXZlLWtleS12YWx1ZQ==";
    private static Profile Profile => Fixture.DefaultProfile with { SubscriptionId = Subscription };

    [Theory]
    [InlineData("Full")]
    [InlineData("Read")]
    [InlineData("FULL")]
    [InlineData("READ")]
    public async Task ServicePermissionCasingIsAcceptedAndPreservedWithoutExposingKeys(string permissions)
    {
        var fixture = new Fixture(_ => Task.FromResult(Json(new
        {
            keys = new[] { new { keyName = "key1", value = Secret, permissions } }
        })), profile: Profile);
        var processor = Processor(fixture);
        await processor.ProcessAsync();
        var key = Assert.Single(Assert.IsType<StorageAccountKey[]>(Assert.Single(fixture.Output)));
        Assert.Equal(permissions, key.Permissions);
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExactArmWireAndCollectionAreAvailableForCopySourceLookup()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("management.azure.com", request.RequestUri!.IdnHost);
            Assert.Equal($"/subscriptions/{Subscription}/resourceGroups/rg_(one)/providers/Microsoft.Storage/storageAccounts/account123/listKeys",
                Uri.UnescapeDataString(request.RequestUri.AbsolutePath));
            Assert.Equal("2025-08-01", BlobWorkflowTests.Query(request)["api-version"]);
            Assert.Null(BlobWorkflowTests.Query(request)["$expand"]);
            Assert.Null(request.Content);
            Assert.True(request.HasSensitiveDiagnostics());
            return Task.FromResult(Keys());
        }, profile: Profile);
        var processor = Processor(fixture);
        await processor.ProcessAsync();
        StorageAccountKey[] keys = Assert.IsType<StorageAccountKey[]>(Assert.Single(fixture.Output));
        Assert.Equal(new[] { "key1", "key2" }, keys.Select(key => key.KeyName));
        Assert.All(keys, key =>
        {
            Assert.Equal(Secret, key.Value); Assert.Equal("Full", key.Permissions);
            Assert.DoesNotContain(Secret, key.ToString());
            Assert.Contains(Secret, JsonSerializer.Serialize(key));
        });
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T00:00:00Z"), keys[0].CreationTime);
        Assert.Null(keys[1].CreationTime);
        var token = Assert.Single(fixture.TokenRequests);
        Assert.Equal(new[] { "https://management.azure.com/.default" }, token.Scopes);
        Assert.Same(fixture.Profile, token.Profile);
        Assert.DoesNotContain(fixture.Messages, message => message.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExplicitSubscriptionDoesNotMutateSelectedProfile()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Contains(OtherSubscription, request.RequestUri!.AbsolutePath);
            return Task.FromResult(Keys());
        }, profile: Profile);
        var processor = Processor(fixture);
        processor.SubscriptionId = OtherSubscription;
        await processor.ProcessAsync();
        Assert.Equal(Subscription, fixture.Profile.SubscriptionId);
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("DEV")]
    [InlineData(" dev ")]
    [InlineData("   ")]
    public async Task ProfileNameDoesNotOverrideTheResolvedProfile(string requested)
    {
        var fixture = new Fixture(_ => Task.FromResult(Keys()), profile: Profile with { Name = "dev" });
        var processor = Processor(fixture);
        processor.Profile = requested;
        await processor.ProcessAsync();
        Assert.Equal("dev", Assert.Single(fixture.TokenRequests).Profile.Name);
    }

    [Fact]
    public async Task EmptyCollectionIsReturnedForCopySourceLookup()
    {
        var fixture = new Fixture(_ => Task.FromResult(Json(new { keys = Array.Empty<object>() })), profile: Profile);
        var processor = Processor(fixture);
        await processor.ProcessAsync();
        Assert.Empty(Assert.IsType<StorageAccountKey[]>(Assert.Single(fixture.Output)));
    }

    [Theory]
    [InlineData("subscription")]
    [InlineData("no-subscription")]
    [InlineData("zero-subscription")]
    [InlineData("resource-group")]
    [InlineData("resource-group-dot")]
    [InlineData("account")]
    [InlineData("data-context")]
    public async Task InvalidInputsCannotAcquireTokens(string invalid)
    {
        var fixture = new Fixture(profile: invalid == "no-subscription" ? Fixture.DefaultProfile : Profile);
        var processor = Processor(fixture);
        switch (invalid)
        {
            case "subscription": processor.SubscriptionId = "bad"; break;
            case "zero-subscription": processor.SubscriptionId = Guid.Empty.ToString(); break;
            case "resource-group": processor.ResourceGroupName = "rg/other"; break;
            case "resource-group-dot": processor.ResourceGroupName = "rg."; break;
            case "account": processor.AccountName = "Account"; break;
            case "data-context": processor.Context = new StorageContext("fixture", fixture.Profile); break;
        }
        await Assert.ThrowsAnyAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("invalid-json")]
    [InlineData("null-key")]
    [InlineData("key-name")]
    [InlineData("value")]
    [InlineData("permissions")]
    [InlineData("date")]
    [InlineData("duplicate")]
    [InlineData("status")]
    public async Task InvalidResponsesNeverLeakSecretsOrEmitPartialKeyOutput(string damage)
    {
        var fixture = new Fixture(_ =>
        {
            if (damage == "invalid-json") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Secret) });
            if (damage == "missing") return Task.FromResult(Json(new { secret = Secret }));
            if (damage == "null") return Task.FromResult(Json(new { keys = (object?)null }));
            if (damage == "status") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            var good = new Dictionary<string, object?> { ["keyName"] = "key1", ["value"] = Secret, ["permissions"] = "Full" };
            var bad = new Dictionary<string, object?>(good) { ["keyName"] = "key2" };
            switch (damage)
            {
                case "key-name": bad["keyName"] = null; break;
                case "value": bad["value"] = "private-value"; break;
                case "permissions": bad["permissions"] = Secret; break;
                case "date": bad["creationTime"] = Secret; break;
                case "duplicate": bad["keyName"] = "KEY1"; break;
            }
            return Task.FromResult(Json(new { keys = new object?[] { good, damage == "null-key" ? null : bad } }));
        }, profile: Profile);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessAsync());
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.DoesNotContain("private-value", error.ToString());
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Messages, item => item.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ServiceFailuresRemainFailuresWithoutAuthenticationFallback(HttpStatusCode status)
    {
        var fixture = new Fixture(_ => Task.FromResult(BlobWorkflowTests.Error(status, "AuthorizationFailure")), profile: Profile);
        await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        Assert.All(fixture.TokenRequests, request => Assert.Equal(new[] { "https://management.azure.com/.default" }, request.Scopes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAfterResponseNeverEmitsKeys(bool before)
    {
        using var cancellation = new CancellationTokenSource();
        if (before) cancellation.Cancel();
        var fixture = new Fixture(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(Keys());
        }, profile: Profile, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(fixture).ProcessAsync());
        Assert.Empty(fixture.Output);
        if (before) Assert.Empty(fixture.TokenRequests);
    }

    private static ListStorageAccountKeysProcessor Processor(Fixture fixture)
    {
        var processor = new ListStorageAccountKeysProcessor { ResourceGroupName = "rg_(one)", AccountName = "account123" };
        processor.Init(fixture.Context);
        return processor;
    }

    private static HttpResponseMessage Keys() => Json(new
    {
        keys = new object[]
        {
            new { keyName = "key1", value = Secret, permissions = "Full", creationTime = "2026-10-20T00:00:00Z" },
            new { keyName = "key2", value = Secret, permissions = "Full" }
        }
    });
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
