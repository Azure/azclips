using System.Net.Http;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public partial class CreateStorageAccountsProcessor : StorageProcessorBase<StorageAccountCreateParameters, StorageAccount>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public IDictionary<string, string>? Tags { get; set; }
    public string? AccessTier { get; set; }
    public bool? EnableHttpsTrafficOnly { get; set; }
    public bool? AllowBlobPublicAccess { get; set; }
    public string? MinimumTlsVersion { get; set; }
    public string? PublicNetworkAccess { get; set; }
    public bool? EnableHierarchicalNamespace { get; set; }
    public bool? EnableSftp { get; set; }
    public bool? EnableLocalUser { get; set; }
    public bool? EnableNfsV3 { get; set; }
    public string? DnsEndpointType { get; set; }
    public IList<string>? Zones { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";

    protected override StorageAccountCreateParameters? CreateRequestBody() => new StorageAccountCreateParameters
    {
        Sku = new Sku { Name = SkuName },
        Kind = Kind,
        Location = Location,
        Tags = Tags,
        Zones = Zones,
        Properties = new StorageAccountPropertiesCreateParameters
        {
            AccessTier = string.IsNullOrWhiteSpace(AccessTier) ? null : AccessTier,
            EnableHttpsTrafficOnly = EnableHttpsTrafficOnly,
            AllowBlobPublicAccess = AllowBlobPublicAccess,
            MinimumTlsVersion = string.IsNullOrWhiteSpace(MinimumTlsVersion) ? null : MinimumTlsVersion,
            PublicNetworkAccess = string.IsNullOrWhiteSpace(PublicNetworkAccess) ? null : PublicNetworkAccess,
            IsHnsEnabled = EnableHierarchicalNamespace,
            IsSftpEnabled = EnableSftp,
            IsLocalUserEnabled = EnableLocalUser,
            EnableNfsV3 = EnableNfsV3,
            DnsEndpointType = string.IsNullOrWhiteSpace(DnsEndpointType) ? null : DnsEndpointType,
        },
    };

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
