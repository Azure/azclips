// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManagementModels = Microsoft.Azure.Azclips.Extensions.Storage.Management.Models;
using ManagementProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Management.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class StorageAccountKey
{
    public string KeyName { get; }
    public string Value { get; }
    public string Permissions { get; }
    public DateTimeOffset? CreationTime { get; }

    internal StorageAccountKey(string keyName, string value, string permissions, DateTimeOffset? creationTime) =>
        (KeyName, Value, Permissions, CreationTime) = (keyName, value, permissions, creationTime);

    public override string ToString() => nameof(StorageAccountKey);
}

public sealed class ListStorageAccountKeysWorkflow : ManagementProcessors.StorageManagementProcessorBase
{
    public string? SubscriptionId { get; set; }
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    protected override HttpMethod Method => HttpMethod.Post;
    protected override string CreateRequestUri() =>
        throw new InvalidOperationException("Account keys are listed through the retained ARM atomic processor.");

    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    private async Task ExecuteAsync()
    {
        var profile = Context?.Profile ?? ResolvedProfile;
        StorageContext.ValidateProfile(profile);
        if (Context is not null && Context.StorageAccountName != AccountName)
            throw new ArgumentException("Account-key lookup must target the selected Storage context's account.");
        Guid subscription = ValidateAddress(profile!, SubscriptionId, ResourceGroupName, AccountName);
        var child = CreateExtendedProcessor<ManagementProcessors.ListKeysStorageAccountsProcessor>(processor =>
        {
            processor.SubscriptionId = subscription.ToString("D"); processor.ResourceGroupName = ResourceGroupName;
            processor.AccountName = AccountName;
            processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
        });
        using var response = await child.SendAsync();
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidDataException("Storage did not return account keys.");
        ManagementModels.StorageAccountListKeysResult? result;
        try { result = await response.Content.ReadFromJsonAsync<ManagementModels.StorageAccountListKeysResult>(JsonOptions, Token); }
        catch (JsonException) { throw InvalidResponse(); }
        var keys = ReadKeys(result);
        Token.ThrowIfCancellationRequested();
        await WriteOutput(keys.ToArray(), Token);
    }

    internal static Guid ValidateAddress(Core.Abstractions.Profile profile, string? subscriptionId, string resourceGroup, string account)
    {
        StorageContext.ValidateProfile(profile);
        if (!Guid.TryParseExact(subscriptionId ?? profile.SubscriptionId, "D", out var subscription) || subscription == Guid.Empty)
            throw new ArgumentException("Specify a valid ARM subscription ID or select a profile with a subscription.");
        if (string.IsNullOrEmpty(resourceGroup) || resourceGroup.Length > 90 || resourceGroup.EndsWith('.') ||
            !Regex.IsMatch(resourceGroup, @"^[-\p{L}\p{M}\p{Nd}_().]+$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Specify a valid ARM resource group name.");
        if (!Regex.IsMatch(account, "^[a-z0-9]{3,24}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Storage account names must contain 3-24 lowercase letters or digits.");
        return subscription;
    }

    private static List<StorageAccountKey> ReadKeys(ManagementModels.StorageAccountListKeysResult? result)
    {
        if (result?.Keys is null) throw InvalidResponse();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new List<StorageAccountKey>(result.Keys.Count);
        foreach (var key in result.Keys)
        {
            if (key is null || string.IsNullOrWhiteSpace(key.KeyName) ||
                key.KeyName.Any(character => character < 0x20 || character > 0x7e) || !names.Add(key.KeyName) ||
                string.IsNullOrEmpty(key.Value) || key.Value.Length > 4096 || key.Permissions is not ("Full" or "Read" or "FULL" or "READ"))
                throw InvalidResponse();
            try
            {
                if (Convert.FromBase64String(key.Value).Length == 0) throw InvalidResponse();
            }
            catch (FormatException) { throw InvalidResponse(); }
            DateTimeOffset? created = null;
            if (key.CreationTime is not null)
            {
                if (!DateTimeOffset.TryParse(key.CreationTime, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                    throw InvalidResponse();
                created = parsed;
            }
            keys.Add(new StorageAccountKey(key.KeyName, key.Value, key.Permissions, created));
        }
        return keys;
    }

    private static InvalidDataException InvalidResponse() =>
        new("Storage returned invalid account-key data. Sensitive response details are omitted.");
}
