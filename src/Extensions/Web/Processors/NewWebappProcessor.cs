// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Web.Models;

namespace Microsoft.Azure.Azclips.Extensions.Web.Processors;

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

	private const string ApiVersion = "2024-11-01";

	protected override async Task<HttpRequestMessage> CreateRequestMessage()
	{
		await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
		string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{Name}?api-version={ApiVersion}";
		Site body = new Site
		{
			Location = Location,
			Kind = Kind,
			Tags = Tags,
			State = State,
			HostNames = HostNames,
			RepositorySiteName = RepositorySiteName,
			UsageState = UsageState,
			Enabled = Enabled,
			EnabledHostNames = EnabledHostNames,
			AvailabilityState = AvailabilityState,
			HostNameSslStates = HostNameSslStates,
			ServerFarmId = ServerFarmId,
			Reserved = Reserved,
			IsXenon = IsXenon,
			HyperV = HyperV,
			LastModifiedTimeUtc = LastModifiedTimeUtc,
			VnetRouteAllEnabled = VnetRouteAllEnabled,
			VnetImagePullEnabled = VnetImagePullEnabled,
			VnetContentShareEnabled = VnetContentShareEnabled,
			SiteConfig = SiteConfig,
			TrafficManagerHostNames = TrafficManagerHostNames,
			ScmSiteAlsoStopped = ScmSiteAlsoStopped,
			TargetSwapSlot = TargetSwapSlot,
			HostingEnvironmentProfile = HostingEnvironmentProfile,
			ClientAffinityEnabled = ClientAffinityEnabled,
			ClientCertEnabled = ClientCertEnabled,
			ClientCertMode = ClientCertMode,
			ClientCertExclusionPaths = ClientCertExclusionPaths,
			HostNamesDisabled = HostNamesDisabled,
			CustomDomainVerificationId = CustomDomainVerificationId,
			OutboundIpAddresses = OutboundIpAddresses,
			PossibleOutboundIpAddresses = PossibleOutboundIpAddresses,
			ContainerSize = ContainerSize,
			DailyMemoryTimeQuota = DailyMemoryTimeQuota,
			SuspendedTill = SuspendedTill,
			MaxNumberOfWorkers = MaxNumberOfWorkers,
			CloningInfo = CloningInfo,
			ResourceGroup = ResourceGroup,
			IsDefaultContainer = IsDefaultContainer,
			DefaultHostName = DefaultHostName,
			SlotSwapStatus = SlotSwapStatus,
			HttpsOnly = HttpsOnly,
			RedundancyMode = RedundancyMode,
			InProgressOperationId = InProgressOperationId,
			PublicNetworkAccess = PublicNetworkAccess,
			StorageAccountRequired = StorageAccountRequired,
			KeyVaultReferenceIdentity = KeyVaultReferenceIdentity,
			VirtualNetworkSubnetId = VirtualNetworkSubnetId,
			ManagedEnvironmentId = ManagedEnvironmentId,
			Identity = Identity,
			ExtendedLocation = ExtendedLocation,
		};
		HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
		{
			Content = JsonContent.Create(body, options: JsonOptions)
		};
		await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
		return request;
	}

}
