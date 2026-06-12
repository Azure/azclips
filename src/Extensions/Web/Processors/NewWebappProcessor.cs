// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Azure.Cirrus.Extensions.Web.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Web.Processors;

public class NewWebappProcessor : WebProcessorBase<Site>
{
	public string Name { get; set; } = string.Empty;

	public string ResourceGroupName { get; set; } = string.Empty;

	public string Location { get; set; } = string.Empty;

	public string? Kind { get; set; }

	public IDictionary<string, string>? Tags { get; set; }

	public string? State { get; set; }

	public IList<string>? HostNames { get; set; }

	public string? RepositorySiteName { get; set; }

	public UsageState? UsageState { get; set; }

	public bool? Enabled { get; set; }

	public IList<string>? EnabledHostNames { get; set; }

	public SiteAvailabilityState? AvailabilityState { get; set; }

	public IList<HostNameSslState>? HostNameSslStates { get; set; }

	public string? ServerFarmId { get; set; }

	public bool? Reserved { get; set; }

	public bool? IsXenon { get; set; }

	public bool? HyperV { get; set; }

	public DateTime? LastModifiedTimeUtc { get; set; }

	public bool? VnetRouteAllEnabled { get; set; }

	public bool? VnetImagePullEnabled { get; set; }

	public bool? VnetContentShareEnabled { get; set; }

	public SiteConfig? SiteConfig { get; set; }

	public IList<string>? TrafficManagerHostNames { get; set; }

	public bool? ScmSiteAlsoStopped { get; set; }

	public string? TargetSwapSlot { get; set; }

	public HostingEnvironmentProfile? HostingEnvironmentProfile { get; set; }

	public bool? ClientAffinityEnabled { get; set; }

	public bool? ClientCertEnabled { get; set; }

	public ClientCertMode? ClientCertMode { get; set; }

	public string? ClientCertExclusionPaths { get; set; }

	public bool? HostNamesDisabled { get; set; }

	public string? CustomDomainVerificationId { get; set; }

	public string? OutboundIpAddresses { get; set; }

	public string? PossibleOutboundIpAddresses { get; set; }

	public int? ContainerSize { get; set; }

	public int? DailyMemoryTimeQuota { get; set; }

	public DateTime? SuspendedTill { get; set; }

	public int? MaxNumberOfWorkers { get; set; }

	public CloningInfo? CloningInfo { get; set; }

	public string? ResourceGroup { get; set; }

	public bool? IsDefaultContainer { get; set; }

	public string? DefaultHostName { get; set; }

	public SlotSwapStatus? SlotSwapStatus { get; set; }

	public bool? HttpsOnly { get; set; }

	public RedundancyMode? RedundancyMode { get; set; }

	public Guid? InProgressOperationId { get; set; }

	public string? PublicNetworkAccess { get; set; }

	public bool? StorageAccountRequired { get; set; }

	public string? KeyVaultReferenceIdentity { get; set; }

	public string? VirtualNetworkSubnetId { get; set; }

	public string? ManagedEnvironmentId { get; set; }

	public ManagedServiceIdentity? Identity { get; set; }

	public ExtendedLocation? ExtendedLocation { get; set; }

	protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";
            VirtualMachine body = new VirtualMachine
            {
                Location = Location,
                Properties = new VirtualMachineProperties
                {
                    HardwareProfile = new HardwareProfile(VmSize),
                    StorageProfile = new StorageProfile
                    {
                        ImageReference = new ImageReference
                        {
                            Publisher = ImagePublisher,
                            Offer = ImageOffer,
                            Sku = ImageSku,
                            Version = ImageVersion
                        }
                    },
                    OSProfile = new OSProfile
                    {
                        ComputerName = Name,
                        AdminUsername = AdminUsername,
                        AdminPassword = AdminPassword,
                        WindowsConfiguration = new WindowsConfiguration()
                    },
                    NetworkProfile = new NetworkProfile
                    {
                        NetworkInterfaces =
                        [
                            new NetworkInterfaceReference { Id = NetworkInterfaceId }
                        ]
                    },
                    SecurityProfile = new SecurityProfile
                    {
                        SecurityType = SecurityType,
                        UefiSettings = new UefiSettings
                        {
                            SecureBootEnabled = SecureBootEnabled,
                            VTpmEnabled = VTpmEnabled
                        }
                    },
                    DiagnosticsProfile = new DiagnosticsProfile
                    {
                        BootDiagnostics = new BootDiagnostics { Enabled = BootDiagnosticsEnabled }
                    }
                }
            };
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
                      }

}
