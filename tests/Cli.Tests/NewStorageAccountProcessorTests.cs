// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="NewStorageAccountProcessor_Customized"/>. Two things are pinned here:
/// the shape of the create request body, and the cross-field rules the processor enforces
/// before it is built.
/// <para>
/// The body assertions matter because unset options must be omitted rather than sent: several
/// service defaults are not the CLR default (for example supportsHttpsTrafficOnly defaults to
/// true), so serializing an unspecified option would silently change the created account.
/// Several members also use wire names that differ from the Azure SDK aliases
/// (<c>keyvaultproperties</c>, <c>keyname</c>, <c>identity</c>, and <c>value</c> on an IP rule);
/// sending the alias instead is accepted by ARM and then ignored, so only an assertion on the
/// serialized JSON catches it.
/// </para>
/// </summary>
public class NewStorageAccountProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string ResourceGroupName = "my-rg";
    private const string AccountName = "mystorageaccount";
    private const string Location = "eastus";
    private const string SkuName = "Standard_LRS";
    private const string Kind = "StorageV2";

    /// <summary>
    /// Terminal long-running-operation payload. The processor polls until it sees a terminal
    /// provisioningState, so every response in these tests has to carry one or the poll loop
    /// would never exit.
    /// </summary>
    private const string SucceededAccount = """
        {
          "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/mystorageaccount",
          "name": "mystorageaccount",
          "type": "Microsoft.Storage/storageAccounts",
          "location": "eastus",
          "kind": "StorageV2",
          "sku": { "name": "Standard_LRS", "tier": "Standard" },
          "properties": { "provisioningState": "Succeeded" }
        }
        """;

    private static NewStorageAccountProcessor_Customized CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        var processor = new NewStorageAccountProcessor_Customized();
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = ResourceGroupName;
        processor.AccountName = AccountName;
        processor.Location = Location;
        processor.SkuName = SkuName;
        processor.Kind = Kind;
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// Runs the processor and returns the body of the first request, which is the create PUT.
    /// Later requests are the long-running-operation polls and carry no body.
    /// </summary>
    private static async Task<JsonElement> CaptureCreateBodyAsync(Action<NewStorageAccountProcessor_Customized> configure)
    {
        string? captured = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            captured ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(SucceededAccount);
        });

        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        configure(processor);
        await processor.ProcessAsync();

        Assert.NotNull(captured);
        return JsonDocument.Parse(captured!).RootElement.Clone();
    }

    /// <summary>
    /// Runs the processor expecting the request never to be built. Asserting that nothing was
    /// sent is the point: these rules exist so an invalid combination fails locally instead of
    /// reaching the service.
    /// </summary>
    private static async Task<ArgumentException> CaptureValidationErrorAsync(Action<NewStorageAccountProcessor_Customized> configure)
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(SucceededAccount));
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());
        configure(processor);
        var error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
        Assert.Equal(0, pipeline.SendCount);
        return error;
    }

    private static JsonElement Properties(JsonElement body) => body.GetProperty("properties");

    // ── Request shape ──

    [Fact]
    public async Task CreateRequest_WithOnlyRequiredOptions_SendsMinimalBody()
    {
        var body = await CaptureCreateBodyAsync(_ => { });

        Assert.Equal(Kind, body.GetProperty("kind").GetString());
        Assert.Equal(Location, body.GetProperty("location").GetString());
        Assert.Equal(SkuName, body.GetProperty("sku").GetProperty("name").GetString());

        // Nothing else may appear: an empty properties bag or an explicit null would override
        // service defaults for options the caller never mentioned.
        Assert.Equal(3, body.EnumerateObject().Count());
        Assert.False(body.TryGetProperty("properties", out _));
        Assert.False(body.TryGetProperty("tags", out _));
        Assert.False(body.TryGetProperty("identity", out _));

        // tier is read-only and derived by the service from the SKU name.
        Assert.False(body.GetProperty("sku").TryGetProperty("tier", out _));
    }

    [Fact]
    public async Task CreateRequest_WithSuppliedOptions_SendsOnlyThoseProperties()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.AccessTier = "Cool";
            p.SupportsHttpsTrafficOnly = "false";
        });

        var properties = Properties(body);
        Assert.Equal("Cool", properties.GetProperty("accessTier").GetString());
        Assert.False(properties.GetProperty("supportsHttpsTrafficOnly").GetBoolean());

        // An unsupplied tri-state option stays absent rather than defaulting to false.
        Assert.Equal(2, properties.EnumerateObject().Count());
        Assert.False(properties.TryGetProperty("allowBlobPublicAccess", out _));
        Assert.False(properties.TryGetProperty("minimumTlsVersion", out _));
    }

    [Fact]
    public async Task CreateRequest_WithTags_SendsKeyValueMap()
    {
        var body = await CaptureCreateBodyAsync(p => p.Tags = ["env=dev", "owner=platform"]);

        var tags = body.GetProperty("tags");
        Assert.Equal("dev", tags.GetProperty("env").GetString());
        Assert.Equal("platform", tags.GetProperty("owner").GetString());
    }

    [Fact]
    public async Task CreateRequest_TagValueContainingEquals_SplitsOnFirstSeparatorOnly()
    {
        var body = await CaptureCreateBodyAsync(p => p.Tags = ["connection=a=b=c"]);

        Assert.Equal("a=b=c", body.GetProperty("tags").GetProperty("connection").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithEnvelopeOptions_SendsThemOutsideProperties()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EdgeZone = "microsoftlosangeles1";
            p.Zones = ["1", "2"];
            p.ZonePlacementPolicy = "Any";
        });

        // EdgeZone is the only extended-location type the service defines, so it is set by the
        // processor rather than exposed as an option.
        var extendedLocation = body.GetProperty("extendedLocation");
        Assert.Equal("microsoftlosangeles1", extendedLocation.GetProperty("name").GetString());
        Assert.Equal("EdgeZone", extendedLocation.GetProperty("type").GetString());

        Assert.Equal(["1", "2"], body.GetProperty("zones").EnumerateArray().Select(z => z.GetString()!).ToArray());
        Assert.Equal("Any", body.GetProperty("placement").GetProperty("zonePlacementPolicy").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithSystemAssignedIdentity_OmitsUserAssignedMap()
    {
        var body = await CaptureCreateBodyAsync(p => p.IdentityType = "SystemAssigned");

        var identity = body.GetProperty("identity");
        Assert.Equal("SystemAssigned", identity.GetProperty("type").GetString());
        Assert.False(identity.TryGetProperty("userAssignedIdentities", out _));
    }

    [Fact]
    public async Task CreateRequest_WithUserAssignedIdentity_SendsResourceIdKeyedMap()
    {
        const string identityId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi";
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.IdentityType = "UserAssigned";
            p.UserIdentityId = identityId;
        });

        var identity = body.GetProperty("identity");
        Assert.Equal("UserAssigned", identity.GetProperty("type").GetString());

        // The service expects the identity resource id as the key and an empty object as the value.
        var entry = Assert.Single(identity.GetProperty("userAssignedIdentities").EnumerateObject().ToList());
        Assert.Equal(identityId, entry.Name);
        Assert.Empty(entry.Value.EnumerateObject().ToList());
    }

    [Fact]
    public async Task CreateRequest_UserIdentityIgnoredWhenTypeDoesNotRequestIt()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.IdentityType = "SystemAssigned";
            p.UserIdentityId = "/subscriptions/s/ids/mi";
        });

        // The service rejects the map unless the type asks for a user-assigned identity.
        Assert.False(body.GetProperty("identity").TryGetProperty("userAssignedIdentities", out _));
    }

    [Fact]
    public async Task CreateRequest_SasPolicyPeriodOnly_DefaultsActionToLog()
    {
        var body = await CaptureCreateBodyAsync(p => p.SasExpirationPeriod = "90.00:00:00");

        var sasPolicy = Properties(body).GetProperty("sasPolicy");
        Assert.Equal("90.00:00:00", sasPolicy.GetProperty("sasExpirationPeriod").GetString());

        // Both members are required by the service, so the action is defaulted rather than omitted.
        Assert.Equal("Log", sasPolicy.GetProperty("expirationAction").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithKeyPolicy_SendsNumericPeriod()
    {
        var body = await CaptureCreateBodyAsync(p => p.KeyExpirationPeriodInDays = "60");

        var keyPolicy = Properties(body).GetProperty("keyPolicy");
        Assert.Equal(JsonValueKind.Number, keyPolicy.GetProperty("keyExpirationPeriodInDays").ValueKind);
        Assert.Equal(60, keyPolicy.GetProperty("keyExpirationPeriodInDays").GetInt32());
    }

    [Fact]
    public async Task CreateRequest_WithImmutability_NestsThePolicy()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EnableAlw = "true";
            p.ImmutabilityPeriodSinceCreationInDays = "30";
            p.ImmutabilityPolicyState = "Unlocked";
            p.AllowProtectedAppendWrites = "true";
        });

        var immutability = Properties(body).GetProperty("immutableStorageWithVersioning");
        Assert.True(immutability.GetProperty("enabled").GetBoolean());

        var policy = immutability.GetProperty("immutabilityPolicy");
        Assert.Equal(30, policy.GetProperty("immutabilityPeriodSinceCreationInDays").GetInt32());
        Assert.Equal("Unlocked", policy.GetProperty("state").GetString());
        Assert.True(policy.GetProperty("allowProtectedAppendWrites").GetBoolean());
    }

    [Fact]
    public async Task CreateRequest_EnableAlwAlone_OmitsThePolicy()
    {
        var body = await CaptureCreateBodyAsync(p => p.EnableAlw = "true");

        var immutability = Properties(body).GetProperty("immutableStorageWithVersioning");
        Assert.True(immutability.GetProperty("enabled").GetBoolean());
        Assert.False(immutability.TryGetProperty("immutabilityPolicy", out _));
    }

    [Fact]
    public async Task CreateRequest_WithRoutingPreference_SendsSuppliedMembersOnly()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.RoutingChoice = "MicrosoftRouting";
            p.PublishMicrosoftEndpoints = "true";
        });

        var routing = Properties(body).GetProperty("routingPreference");
        Assert.Equal("MicrosoftRouting", routing.GetProperty("routingChoice").GetString());
        Assert.True(routing.GetProperty("publishMicrosoftEndpoints").GetBoolean());
        Assert.False(routing.TryGetProperty("publishInternetEndpoints", out _));
    }

    [Fact]
    public async Task CreateRequest_WithNetworkRules_UsesServiceWireNames()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.DefaultAction = "Deny";
            p.Bypass = ["Logging", "Metrics"];
            p.IpAddress = ["13.64.0.0/16"];
        });

        var acls = Properties(body).GetProperty("networkAcls");
        Assert.Equal("Deny", acls.GetProperty("defaultAction").GetString());

        // bypass is a comma-separated string on the wire, not an array.
        Assert.Equal("Logging, Metrics", acls.GetProperty("bypass").GetString());

        var rule = Assert.Single(acls.GetProperty("ipRules").EnumerateArray().ToList());
        Assert.Equal("13.64.0.0/16", rule.GetProperty("value").GetString());
        Assert.Equal("Allow", rule.GetProperty("action").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithIpv6Rules_SendsThemSeparately()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.DefaultAction = "Deny";
            p.Ipv6Address = ["2001:db8::/32"];
        });

        var acls = Properties(body).GetProperty("networkAcls");
        Assert.False(acls.TryGetProperty("ipRules", out _));
        var rule = Assert.Single(acls.GetProperty("ipv6Rules").EnumerateArray().ToList());
        Assert.Equal("2001:db8::/32", rule.GetProperty("value").GetString());
    }

    [Fact]
    public async Task CreateRequest_SubnetName_IsComposedIntoAResourceId()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.DefaultAction = "Deny";
            p.Subnet = "mysubnet";
            p.VnetName = "myvnet";
        });

        var rule = Assert.Single(
            Properties(body).GetProperty("networkAcls").GetProperty("virtualNetworkRules").EnumerateArray().ToList());

        // Composed locally from the subscription and resource group; no lookup is performed.
        Assert.Equal(
            $"/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}"
            + "/providers/Microsoft.Network/virtualNetworks/myvnet/subnets/mysubnet",
            rule.GetProperty("id").GetString());
    }

    [Fact]
    public async Task CreateRequest_SubnetResourceId_IsSentUnchanged()
    {
        const string subnetId = "/subscriptions/other/resourceGroups/other-rg/providers/Microsoft.Network/virtualNetworks/vnet/subnets/subnet";
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.DefaultAction = "Deny";
            p.Subnet = subnetId;
        });

        var rule = Assert.Single(
            Properties(body).GetProperty("networkAcls").GetProperty("virtualNetworkRules").EnumerateArray().ToList());
        Assert.Equal(subnetId, rule.GetProperty("id").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithKeyVaultKey_InfersKeySourceAndUsesLowerCaseNames()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EncryptionKeyVault = "https://myvault.vault.azure.net/";
            p.EncryptionKeyName = "mykey";
        });

        var encryption = Properties(body).GetProperty("encryption");

        // The key source is not supplied above; a key vault key implies it.
        Assert.Equal("Microsoft.Keyvault", encryption.GetProperty("keySource").GetString());

        var keyVault = encryption.GetProperty("keyvaultproperties");
        Assert.Equal("mykey", keyVault.GetProperty("keyname").GetString());
        Assert.Equal("https://myvault.vault.azure.net/", keyVault.GetProperty("keyvaulturi").GetString());

        // The service requires a services entry whenever encryption is supplied.
        Assert.True(encryption.GetProperty("services").GetProperty("blob").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task CreateRequest_WithEncryptionIdentity_UsesTheIdentityWireName()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EncryptionKeyVault = "https://myvault.vault.azure.net/";
            p.EncryptionKeyName = "mykey";
            p.KeyVaultUserIdentityId = "/subscriptions/s/ids/mi";
            p.KeyVaultFederatedClientId = "00000000-0000-0000-0000-000000000000";
        });

        // Serialized as "identity", not the SDK's "encryptionIdentity" alias.
        var identity = Properties(body).GetProperty("encryption").GetProperty("identity");
        Assert.Equal("/subscriptions/s/ids/mi", identity.GetProperty("userAssignedIdentity").GetString());
        Assert.Equal("00000000-0000-0000-0000-000000000000", identity.GetProperty("federatedIdentityClientId").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithServiceKeyTypes_ReplacesTheDefaultBlobService()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EncryptionKeyTypeForTable = "Account";
            p.EncryptionKeyTypeForQueue = "Account";
        });

        var encryption = Properties(body).GetProperty("encryption");
        Assert.Equal("Microsoft.Storage", encryption.GetProperty("keySource").GetString());

        var services = encryption.GetProperty("services");
        Assert.Equal("Account", services.GetProperty("table").GetProperty("keyType").GetString());
        Assert.Equal("Account", services.GetProperty("queue").GetProperty("keyType").GetString());

        // blob is only the fallback used when no service key type was requested.
        Assert.False(services.TryGetProperty("blob", out _));
    }

    [Fact]
    public async Task CreateRequest_WithoutEncryptionOptions_OmitsEncryption()
    {
        var body = await CaptureCreateBodyAsync(p => p.AccessTier = "Hot");

        // Azure PowerShell only sends encryption when asked; the service otherwise applies its
        // own defaults. azure-cli always sends it, which is not followed here.
        Assert.False(Properties(body).TryGetProperty("encryption", out _));
    }

    [Fact]
    public async Task CreateRequest_WithActiveDirectory_SendsAdOptionAndProperties()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EnableFilesAdds = "true";
            p.DomainName = "contoso.com";
            p.NetBiosDomainName = "contoso";
            p.ForestName = "contoso.com";
            p.DomainGuid = "00000000-0000-0000-0000-000000000000";
            p.DomainSid = "S-1-5-21-1";
            p.AzureStorageSid = "S-1-5-21-2";
        });

        var auth = Properties(body).GetProperty("azureFilesIdentityBasedAuthentication");
        Assert.Equal("AD", auth.GetProperty("directoryServiceOptions").GetString());
        Assert.Equal("contoso.com", auth.GetProperty("activeDirectoryProperties").GetProperty("domainName").GetString());
    }

    [Fact]
    public async Task CreateRequest_WithKerberos_SendsOnlyDomainNameAndGuid()
    {
        var body = await CaptureCreateBodyAsync(p =>
        {
            p.EnableFilesAadkerb = "true";
            p.DomainName = "contoso.com";
        });

        var auth = Properties(body).GetProperty("azureFilesIdentityBasedAuthentication");
        Assert.Equal("AADKERB", auth.GetProperty("directoryServiceOptions").GetString());

        var directoryProperties = auth.GetProperty("activeDirectoryProperties");
        Assert.Equal("contoso.com", directoryProperties.GetProperty("domainName").GetString());
        Assert.Single(directoryProperties.EnumerateObject().ToList());
    }

    [Fact]
    public async Task CreateRequest_DisabledFilesAuthentication_SendsNoneOption()
    {
        var body = await CaptureCreateBodyAsync(p => p.EnableFilesAadds = "false");

        var auth = Properties(body).GetProperty("azureFilesIdentityBasedAuthentication");
        Assert.Equal("None", auth.GetProperty("directoryServiceOptions").GetString());
    }

    [Fact]
    public async Task CreateRequest_SharePermissionWithoutEnabler_CreatesParentWithNoneOption()
    {
        var body = await CaptureCreateBodyAsync(p => p.DefaultSharePermission = "StorageFileDataSmbShareReader");

        // directoryServiceOptions is required by the service, so the parent is created with None.
        var auth = Properties(body).GetProperty("azureFilesIdentityBasedAuthentication");
        Assert.Equal("None", auth.GetProperty("directoryServiceOptions").GetString());
        Assert.Equal("StorageFileDataSmbShareReader", auth.GetProperty("defaultSharePermission").GetString());
    }

    [Fact]
    public async Task CreateRequest_SmbOAuthWithoutEnabler_CreatesParentWithNoneOption()
    {
        var body = await CaptureCreateBodyAsync(p => p.EnableSmbOauth = "true");

        var auth = Properties(body).GetProperty("azureFilesIdentityBasedAuthentication");
        Assert.Equal("None", auth.GetProperty("directoryServiceOptions").GetString());
        Assert.True(auth.GetProperty("smbOAuthSettings").GetProperty("isSmbOAuthEnabled").GetBoolean());
    }

    [Fact]
    public async Task CreateRequest_TargetsTheAccountResourceUri()
    {
        HttpRequestMessage? first = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            first ??= request;
            return Json(SucceededAccount);
        });

        await CreateProcessor(pipeline, new FakeOutputHandler()).ProcessAsync();

        Assert.NotNull(first);
        Assert.Equal(HttpMethod.Put, first!.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourceGroups/{ResourceGroupName}"
            + $"/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01",
            first.RequestUri!.AbsoluteUri);
    }

    // Profile fallback for SubscriptionId is no longer the processor's job: ArgumentParsingService
    // applies it from the active profile at binding time (Azure/azclips#786). The end-to-end
    // behaviour is covered by StorageAccountCreateScenarioTests, which omits the subscription on
    // both surfaces and asserts it is resolved into the request URI.

    // ── Response handling ──

    [Fact]
    public async Task ProcessAsync_PollsUntilProvisioningSucceeds_AndWritesTheAccount()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(SucceededAccount));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        // Create is a long-running operation: the initial PUT is followed by polling and a final
        // read, so more than one request is expected.
        Assert.True(pipeline.SendCount > 1, $"expected polling, saw {pipeline.SendCount} request(s)");

        var account = Assert.IsType<StorageAccount>(Assert.Single(output.WrittenValues));
        Assert.Equal(AccountName, account.Name);
        Assert.Equal("Succeeded", account.Properties?.ProvisioningState);
        Assert.Equal("Standard", account.Sku?.Tier);
    }

    [Fact]
    public async Task ProcessAsync_ResponseFieldsOutsideTheModel_AreIgnored()
    {
        // The service returns considerably more than the model declares. Unknown members must not
        // fail deserialization, otherwise every new service field would break the command.
        const string body = """
            {
              "name": "mystorageaccount",
              "location": "eastus",
              "kind": "StorageV2",
              "someBrandNewServiceField": { "nested": true },
              "properties": { "provisioningState": "Succeeded", "anotherNewField": 42 }
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();

        await CreateProcessor(pipeline, output).ProcessAsync();

        var account = Assert.IsType<StorageAccount>(Assert.Single(output.WrittenValues));
        Assert.Equal(AccountName, account.Name);
    }

    [Fact]
    public async Task ProcessAsync_SurfacesIdentityAndNestedResponseValues()
    {
        // Regression: identity was absent from the response model, so a system-assigned identity
        // was created but never shown in the command output.
        const string body = """
            {
              "name": "mystorageaccount",
              "location": "eastus",
              "kind": "StorageV2",
              "identity": { "type": "SystemAssigned", "principalId": "principal-1", "tenantId": "tenant-1" },
              "properties": {
                "provisioningState": "Succeeded",
                "minimumTlsVersion": "TLS1_2",
                "primaryEndpoints": { "blob": "https://mystorageaccount.blob.core.windows.net/" },
                "networkAcls": { "defaultAction": "Deny", "bypass": "Logging" }
              }
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();

        await CreateProcessor(pipeline, output).ProcessAsync();

        var account = Assert.IsType<StorageAccount>(Assert.Single(output.WrittenValues));
        Assert.Equal("SystemAssigned", account.Identity?.Type);
        Assert.Equal("principal-1", account.Identity?.PrincipalId);
        Assert.Equal("TLS1_2", account.Properties?.MinimumTlsVersion);
        Assert.Equal("https://mystorageaccount.blob.core.windows.net/", account.Properties?.PrimaryEndpoints?.Blob);
        Assert.Equal("Deny", account.Properties?.NetworkAcls?.DefaultAction);
    }

    [Fact]
    public async Task ProcessAsync_TerminalFailure_SurfacesTheArmErrorDetail()
    {
        // A generic message with only the response object gives users nothing to act on, so the
        // ARM error is captured during the status peek while the body is still readable.
        const string failed = """
            {
              "name": "mystorageaccount",
              "location": "eastus",
              "kind": "StorageV2",
              "properties": { "provisioningState": "Failed" },
              "error": { "code": "StorageAccountAlreadyTaken", "message": "The storage account named mystorageaccount is already taken." }
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(failed));
        var processor = CreateProcessor(pipeline, new FakeOutputHandler());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Contains("StorageAccountAlreadyTaken", error.Message);
        Assert.Contains("already taken", error.Message);
        Assert.Contains("storageAccounts/mystorageaccount", error.Message);
    }

    // ── Validation ──

    [Fact]
    public async Task CreateRequest_MalformedTag_Throws()
    {
        var error = await CaptureValidationErrorAsync(p => p.Tags = ["notavalidtag"]);
        Assert.Contains("KEY=VALUE", error.Message);
    }

    [Fact]
    public async Task CreateRequest_SasExpirationActionWithoutPeriod_Throws()
    {
        var error = await CaptureValidationErrorAsync(p => p.SasExpirationAction = "Block");
        Assert.Contains("SasExpirationPeriod", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("false")]
    public async Task CreateRequest_ImmutabilityPolicyWithoutEnableAlw_Throws(string enableAlw)
    {
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.EnableAlw = enableAlw;
            p.ImmutabilityPolicyState = "Locked";
        });
        Assert.Contains("EnableAlw", error.Message);
    }

    [Fact]
    public async Task CreateRequest_NetworkRuleWithoutDefaultAction_Throws()
    {
        var error = await CaptureValidationErrorAsync(p => p.Bypass = ["Logging"]);
        Assert.Contains("DefaultAction", error.Message);
    }

    [Fact]
    public async Task CreateRequest_UnknownBypassValue_Throws()
    {
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.DefaultAction = "Deny";
            p.Bypass = ["Logging", "Telemetry"];
        });
        Assert.Contains("Telemetry", error.Message);
    }

    [Fact]
    public async Task CreateRequest_SubnetNameWithoutVnetName_Throws()
    {
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.DefaultAction = "Deny";
            p.Subnet = "mysubnet";
        });
        Assert.Contains("VnetName", error.Message);
    }

    [Fact]
    public async Task CreateRequest_VnetNameWithoutSubnet_Throws()
    {
        // On its own VnetName has nothing to qualify, and it would still mark the properties bag as
        // supplied, sending an otherwise empty object.
        var error = await CaptureValidationErrorAsync(p => p.VnetName = "myvnet");
        Assert.Contains("Subnet", error.Message);
    }

    [Fact]
    public async Task CreateRequest_ActionWithoutARule_Throws()
    {
        // Action qualifies a subnet or IP rule; alone it would be dropped silently.
        var error = await CaptureValidationErrorAsync(p => p.Action = "Allow");
        Assert.Contains("Action", error.Message);
    }

    [Fact]
    public async Task CreateRequest_MoreThanOneFilesAuthenticationEnabler_Throws()
    {
        // All three map onto a single directoryServiceOptions value, so the later one would
        // otherwise silently win.
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.EnableFilesAadds = "true";
            p.EnableFilesAdds = "true";
        });
        Assert.Contains("At most one", error.Message);
    }

    [Fact]
    public async Task CreateRequest_ActiveDirectoryWithoutAllRequiredFields_Throws()
    {
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.EnableFilesAdds = "true";
            p.DomainName = "contoso.com";
        });
        Assert.Contains("NetBiosDomainName", error.Message);
    }

    [Fact]
    public async Task CreateRequest_ActiveDirectoryFieldsWithoutEnabler_Throws()
    {
        var error = await CaptureValidationErrorAsync(p => p.DomainName = "contoso.com");
        Assert.Contains("require", error.Message);
    }

    [Fact]
    public async Task CreateRequest_ActiveDirectoryFieldsWhileDisabling_Throws()
    {
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.EnableFilesAdds = "false";
            p.DomainSid = "S-1-5-21-1";
        });
        Assert.Contains("disabled", error.Message);
    }

    [Theory]
    [InlineData("https://myvault.vault.azure.net/", "", "")]
    [InlineData("", "mykey", "")]
    [InlineData("", "", "v1")]
    public async Task CreateRequest_IncompleteKeyVaultKey_Throws(string vault, string name, string version)
    {
        var error = await CaptureValidationErrorAsync(p =>
        {
            p.EncryptionKeyVault = vault;
            p.EncryptionKeyName = name;
            p.EncryptionKeyVersion = version;
        });
        Assert.Contains("must be specified together", error.Message);
    }

    [Fact]
    public async Task CreateRequest_EncryptionIdentityWithoutKeyVaultKey_Throws()
    {
        var error = await CaptureValidationErrorAsync(p => p.KeyVaultUserIdentityId = "/subscriptions/s/ids/mi");
        Assert.Contains("KeyVaultUserIdentityId", error.Message);
    }

    [Fact]
    public async Task CreateRequest_NonNumericIntegerOption_Throws()
    {
        // int.Parse would surface a bare FormatException naming neither the option nor the value.
        var error = await CaptureValidationErrorAsync(p => p.KeyExpirationPeriodInDays = "foo");
        Assert.Contains("KeyExpirationPeriodInDays", error.Message);
        Assert.Contains("whole number", error.Message);
        Assert.Contains("foo", error.Message);
    }

    [Fact]
    public async Task CreateRequest_NonBooleanOption_Throws()
    {
        // Reachable only if a command table entry omits AllowedValues; the parser rejects it first.
        var error = await CaptureValidationErrorAsync(p => p.IsHnsEnabled = "yes");
        Assert.Contains("IsHnsEnabled", error.Message);
        Assert.Contains("true or false", error.Message);
    }

    [Fact]
    public async Task CreateRequest_KeyVaultSourceWithoutKey_Throws()
    {
        var error = await CaptureValidationErrorAsync(p => p.EncryptionKeySource = "Microsoft.Keyvault");
        Assert.Contains("Microsoft.Keyvault", error.Message);
    }
}
