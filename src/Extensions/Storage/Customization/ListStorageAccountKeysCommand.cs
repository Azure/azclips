// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Management.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class ListStorageAccountKeysCommand : StorageManagementProcessorBase
{
    public string? SubscriptionId { get; set; }
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? ExpandKeyType { get; set; }

    protected override HttpMethod Method => HttpMethod.Post;
    protected override string CreateRequestUri() =>
        throw new InvalidOperationException("Account keys are listed through the account-key workflow.");

    public override Task ProcessAsync() => ProcessWorkflowAsync(async () =>
    {
        if (ExpandKeyType is not null && !string.Equals(ExpandKeyType, "kerb", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("ExpandKeyType supports kerb only.", nameof(ExpandKeyType));
        var child = CreateExtendedProcessor<ListStorageAccountKeysProcessor>(processor =>
        {
            processor.SubscriptionId = SubscriptionId;
            processor.ResourceGroupName = ResourceGroupName;
            processor.AccountName = AccountName;
            processor.ListKerbKey = ExpandKeyType is not null;
            processor.ReturnCollection = true;
            processor.DeferFailureDiagnostics = true;
        });
        await child.ProcessAsync();
    });
}
