// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Azure.Cirrus.Extensions.Web.Models;

/// <summary>
/// A web app, a mobile app backend, or an API app.
/// Generated from Azure REST API specification: Microsoft.Web/AppService/stable/2022-09-01/CommonDefinitions.json#/definitions/Site
/// </summary>
public class Site : Resource
{
    /// <summary>
    /// Current state of the app.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Hostnames associated with the app.
    /// </summary>
    public IList<string>? HostNames { get; set; }

    /// <summary>
    /// Name of the repository site.
    /// </summary>
    public string? RepositorySiteName { get; set; }

    /// <summary>
    /// State indicating whether the app has exceeded its quota usage. Read-only.
    /// </summary>
    public UsageState? UsageState { get; set; }

    /// <summary>
    /// true if the app is enabled; otherwise, false. Setting this value to false disables the app (takes the app offline).
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Enabled hostnames for the app. Hostnames need to be assigned (see HostNames) AND enabled. Otherwise,
    /// the app is not served on those hostnames.
    /// </summary>
    public IList<string>? EnabledHostNames { get; set; }

    /// <summary>
    /// Management information availability state for the app.
    /// </summary>
    public SiteAvailabilityState? AvailabilityState { get; set; }

    /// <summary>
    /// Hostname SSL states are used to manage the SSL bindings for app's hostnames.
    /// </summary>
    public IList<HostNameSslState>? HostNameSslStates { get; set; }

    /// <summary>
    /// Resource ID of the associated App Service plan, formatted as:
    /// "/subscriptions/{subscriptionID}/resourceGroups/{groupName}/providers/Microsoft.Web/serverfarms/{appServicePlanName}".
    /// </summary>
    public string? ServerFarmId { get; set; }

    /// <summary>
    /// true if reserved; otherwise, false.
    /// </summary>
    public bool? Reserved { get; set; }

    /// <summary>
    /// Obsolete: Hyper-V sandbox.
    /// </summary>
    public bool? IsXenon { get; set; }

    /// <summary>
    /// Hyper-V sandbox.
    /// </summary>
    public bool? HyperV { get; set; }

    /// <summary>
    /// Last time the app was modified, in UTC. Read-only.
    /// </summary>
    public DateTime? LastModifiedTimeUtc { get; set; }

    /// <summary>
    /// Virtual Network Route All enabled. This causes all outbound traffic to have Virtual Network Security Groups and User Defined Routes applied.
    /// </summary>
    public bool? VnetRouteAllEnabled { get; set; }

    /// <summary>
    /// To enable pulling image over Virtual Network
    /// </summary>
    public bool? VnetImagePullEnabled { get; set; }

    /// <summary>
    /// To enable accessing content over virtual network
    /// </summary>
    public bool? VnetContentShareEnabled { get; set; }

    /// <summary>
    /// Configuration of the app.
    /// </summary>
    public SiteConfig? SiteConfig { get; set; }

    /// <summary>
    /// Azure Traffic Manager hostnames associated with the app. Read-only.
    /// </summary>
    public IList<string>? TrafficManagerHostNames { get; set; }

    /// <summary>
    /// true to stop SCM (KUDU) site when the app is stopped; otherwise, false. The default is false.
    /// </summary>
    public bool? ScmSiteAlsoStopped { get; set; }

    /// <summary>
    /// Specifies which deployment slot this app will swap into. Read-only.
    /// </summary>
    public string? TargetSwapSlot { get; set; }

    /// <summary>
    /// App Service Environment to use for the app.
    /// </summary>
    public HostingEnvironmentProfile? HostingEnvironmentProfile { get; set; }

    /// <summary>
    /// true to enable client affinity; false to stop sending session affinity cookies, which route client requests in the same session to the same instance.
    /// Default is true.
    /// </summary>
    public bool? ClientAffinityEnabled { get; set; }

    /// <summary>
    /// true to enable client certificate authentication (TLS mutual authentication); otherwise, false.
    /// Default is false.
    /// </summary>
    public bool? ClientCertEnabled { get; set; }

    /// <summary>
    /// This composes with ClientCertEnabled setting.
    /// - ClientCertEnabled: false means ClientCert is ignored.
    /// - ClientCertEnabled: true and ClientCertMode: Required means ClientCert is required.
    /// - ClientCertEnabled: true and ClientCertMode: Optional means ClientCert is optional or accepted.
    /// </summary>
    public ClientCertMode? ClientCertMode { get; set; }

    /// <summary>
    /// Client certificate authentication comma-separated exclusion paths
    /// </summary>
    public string? ClientCertExclusionPaths { get; set; }

    /// <summary>
    /// true to disable the public hostnames of the app; otherwise, false.
    /// If true, the app is only accessible via API management process.
    /// </summary>
    public bool? HostNamesDisabled { get; set; }

    /// <summary>
    /// Unique identifier that verifies the custom domains assigned to the app.
    /// Customer will add this id to a txt record for verification.
    /// </summary>
    public string? CustomDomainVerificationId { get; set; }

    /// <summary>
    /// List of IP addresses that the app uses for outbound connections (e.g. database access).
    /// Includes VIPs from tenants that site can be hosted with current settings. Read-only.
    /// </summary>
    public string? OutboundIpAddresses { get; set; }

    /// <summary>
    /// List of IP addresses that the app uses for outbound connections (e.g. database access).
    /// Includes VIPs from all tenants except dataComponent. Read-only.
    /// </summary>
    public string? PossibleOutboundIpAddresses { get; set; }

    /// <summary>
    /// Size of the function container.
    /// </summary>
    public int? ContainerSize { get; set; }

    /// <summary>
    /// Maximum allowed daily memory-time quota (applicable on dynamic apps only).
    /// </summary>
    public int? DailyMemoryTimeQuota { get; set; }

    /// <summary>
    /// App suspended till in case memory-time quota is exceeded.
    /// </summary>
    public DateTime? SuspendedTill { get; set; }

    /// <summary>
    /// Maximum number of workers. This only applies to Functions container.
    /// </summary>
    public int? MaxNumberOfWorkers { get; set; }

    /// <summary>
    /// If specified during app creation, the app is cloned from a source app.
    /// </summary>
    public CloningInfo? CloningInfo { get; set; }

    /// <summary>
    /// Name of the resource group the app belongs to. Read-only.
    /// </summary>
    public string? ResourceGroup { get; set; }

    /// <summary>
    /// true if the app is a default container; otherwise, false.
    /// </summary>
    public bool? IsDefaultContainer { get; set; }

    /// <summary>
    /// Default hostname of the app. Read-only.
    /// </summary>
    public string? DefaultHostName { get; set; }

    /// <summary>
    /// Status of the last deployment slot swap operation.
    /// </summary>
    public SlotSwapStatus? SlotSwapStatus { get; set; }

    /// <summary>
    /// HttpsOnly: configures a web site to accept only https requests. Issues redirect for http requests
    /// </summary>
    public bool? HttpsOnly { get; set; }

    /// <summary>
    /// Site redundancy mode
    /// </summary>
    public RedundancyMode? RedundancyMode { get; set; }

    /// <summary>
    /// Specifies an operation id if this site has a pending operation.
    /// </summary>
    public Guid? InProgressOperationId { get; set; }

    /// <summary>
    /// Property to allow or block all public traffic. Allowed Values: 'Enabled', 'Disabled' or an empty string.
    /// </summary>
    public string? PublicNetworkAccess { get; set; }

    /// <summary>
    /// Checks if Customer provided storage account is required
    /// </summary>
    public bool? StorageAccountRequired { get; set; }

    /// <summary>
    /// Identity to use for Key Vault Reference authentication.
    /// </summary>
    public string? KeyVaultReferenceIdentity { get; set; }

    /// <summary>
    /// Azure Resource Manager ID of the Virtual network and subnet to be joined by Regional VNET Integration.
    /// This must be of the form /subscriptions/{subscriptionName}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets/{subnetName}
    /// </summary>
    public string? VirtualNetworkSubnetId { get; set; }

    /// <summary>
    /// Azure Resource Manager ID of the customer's selected Managed Environment on which to host this app.
    /// This must be of the form /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/managedEnvironments/{managedEnvironmentName}
    /// </summary>
    public string? ManagedEnvironmentId { get; set; }

    /// <summary>
    /// Managed service identity.
    /// </summary>
    public ManagedServiceIdentity? Identity { get; set; }

    /// <summary>
    /// Extended Location.
    /// </summary>
    public ExtendedLocation? ExtendedLocation { get; set; }
}

/// <summary>
/// Usage state of the app.
/// </summary>
public enum UsageState
{
    /// <summary>
    /// Normal usage state.
    /// </summary>
    Normal,

    /// <summary>
    /// Exceeded quota usage.
    /// </summary>
    Exceeded
}

/// <summary>
/// Site availability state.
/// </summary>
public enum SiteAvailabilityState
{
    /// <summary>
    /// Normal availability state.
    /// </summary>
    Normal,

    /// <summary>
    /// Limited availability state.
    /// </summary>
    Limited,

    /// <summary>
    /// Disaster recovery mode.
    /// </summary>
    DisasterRecoveryMode
}

/// <summary>
/// Client certificate mode.
/// </summary>
public enum ClientCertMode
{
    /// <summary>
    /// Client certificate is required.
    /// </summary>
    Required,

    /// <summary>
    /// Client certificate is optional.
    /// </summary>
    Optional,

    /// <summary>
    /// Client certificate is optional for interactive users.
    /// </summary>
    OptionalInteractiveUser
}

/// <summary>
/// Site redundancy mode.
/// </summary>
public enum RedundancyMode
{
    /// <summary>
    /// No redundancy.
    /// </summary>
    None,

    /// <summary>
    /// Manual redundancy.
    /// </summary>
    Manual,

    /// <summary>
    /// Failover redundancy.
    /// </summary>
    Failover,

    /// <summary>
    /// Active-active redundancy.
    /// </summary>
    ActiveActive,

    /// <summary>
    /// Geo-redundant.
    /// </summary>
    GeoRedundant
}

/// <summary>
/// Hostname SSL state.
/// </summary>
public class HostNameSslState
{
    /// <summary>
    /// Hostname.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// SSL type.
    /// </summary>
    public SslState? SslState { get; set; }

    /// <summary>
    /// Virtual IP address assigned to the hostname if IP based SSL is enabled.
    /// </summary>
    public string? VirtualIP { get; set; }

    /// <summary>
    /// SSL certificate thumbprint.
    /// </summary>
    public string? Thumbprint { get; set; }

    /// <summary>
    /// Set to true to update existing hostname.
    /// </summary>
    public bool? ToUpdate { get; set; }

    /// <summary>
    /// Indicates whether the hostname is a standard or repository hostname.
    /// </summary>
    public HostType? HostType { get; set; }
}

/// <summary>
/// SSL state.
/// </summary>
public enum SslState
{
    /// <summary>
    /// SSL disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// SNI enabled.
    /// </summary>
    SniEnabled,

    /// <summary>
    /// IP based SSL enabled.
    /// </summary>
    IpBasedEnabled
}

/// <summary>
/// Host type.
/// </summary>
public enum HostType
{
    /// <summary>
    /// Standard hostname.
    /// </summary>
    Standard,

    /// <summary>
    /// Repository hostname.
    /// </summary>
    Repository
}

/// <summary>
/// Site configuration.
/// </summary>
public class SiteConfig
{
    /// <summary>
    /// Number of workers.
    /// </summary>
    public int? NumberOfWorkers { get; set; }

    /// <summary>
    /// Default documents.
    /// </summary>
    public IList<string>? DefaultDocuments { get; set; }

    /// <summary>
    /// .NET Framework version.
    /// </summary>
    public string? NetFrameworkVersion { get; set; }

    /// <summary>
    /// Version of PHP.
    /// </summary>
    public string? PhpVersion { get; set; }

    /// <summary>
    /// Version of Python.
    /// </summary>
    public string? PythonVersion { get; set; }

    /// <summary>
    /// Version of Node.js.
    /// </summary>
    public string? NodeVersion { get; set; }

    /// <summary>
    /// Version of PowerShell.
    /// </summary>
    public string? PowerShellVersion { get; set; }

    /// <summary>
    /// Linux App Framework and version
    /// </summary>
    public string? LinuxFxVersion { get; set; }

    /// <summary>
    /// Xenon App Framework and version
    /// </summary>
    public string? WindowsFxVersion { get; set; }

    /// <summary>
    /// true if request tracing is enabled; otherwise, false.
    /// </summary>
    public bool? RequestTracingEnabled { get; set; }

    /// <summary>
    /// Request tracing expiration time.
    /// </summary>
    public DateTime? RequestTracingExpirationTime { get; set; }

    /// <summary>
    /// true if remote debugging is enabled; otherwise, false.
    /// </summary>
    public bool? RemoteDebuggingEnabled { get; set; }

    /// <summary>
    /// Remote debugging version.
    /// </summary>
    public string? RemoteDebuggingVersion { get; set; }

    /// <summary>
    /// true if HTTP logging is enabled; otherwise, false.
    /// </summary>
    public bool? HttpLoggingEnabled { get; set; }

    /// <summary>
    /// Flag to use Managed Identity Creds for ACR pull
    /// </summary>
    public bool? AcrUseManagedIdentityCreds { get; set; }

    /// <summary>
    /// If using user managed identity, the user managed identity ClientId
    /// </summary>
    public string? AcrUserManagedIdentityID { get; set; }

    /// <summary>
    /// HTTP logs directory size limit.
    /// </summary>
    public int? LogsDirectorySizeLimit { get; set; }

    /// <summary>
    /// true if detailed error logging is enabled; otherwise, false.
    /// </summary>
    public bool? DetailedErrorLoggingEnabled { get; set; }

    /// <summary>
    /// Publishing user name.
    /// </summary>
    public string? PublishingUsername { get; set; }

    /// <summary>
    /// Application settings.
    /// </summary>
    public IList<NameValuePair>? AppSettings { get; set; }

    /// <summary>
    /// Application metadata. This property cannot be retrieved, since it may contain secrets.
    /// </summary>
    public IList<NameValuePair>? Metadata { get; set; }

    /// <summary>
    /// Connection strings.
    /// </summary>
    public IList<ConnStringInfo>? ConnectionStrings { get; set; }

    /// <summary>
    /// Handler mappings.
    /// </summary>
    public IList<HandlerMapping>? HandlerMappings { get; set; }

    /// <summary>
    /// Document root.
    /// </summary>
    public string? DocumentRoot { get; set; }

    /// <summary>
    /// SCM type.
    /// </summary>
    public ScmType? ScmType { get; set; }

    /// <summary>
    /// true to use 32-bit worker process; otherwise, false.
    /// </summary>
    public bool? Use32BitWorkerProcess { get; set; }

    /// <summary>
    /// true if WebSocket is enabled; otherwise, false.
    /// </summary>
    public bool? WebSocketsEnabled { get; set; }

    /// <summary>
    /// true if Always On is enabled; otherwise, false.
    /// </summary>
    public bool? AlwaysOn { get; set; }

    /// <summary>
    /// Java version.
    /// </summary>
    public string? JavaVersion { get; set; }

    /// <summary>
    /// Java container.
    /// </summary>
    public string? JavaContainer { get; set; }

    /// <summary>
    /// Java container version.
    /// </summary>
    public string? JavaContainerVersion { get; set; }

    /// <summary>
    /// App command line to launch.
    /// </summary>
    public string? AppCommandLine { get; set; }

    /// <summary>
    /// Managed pipeline mode.
    /// </summary>
    public ManagedPipelineMode? ManagedPipelineMode { get; set; }

    /// <summary>
    /// Virtual applications.
    /// </summary>
    public IList<VirtualApplication>? VirtualApplications { get; set; }

    /// <summary>
    /// Site load balancing.
    /// </summary>
    public SiteLoadBalancing? LoadBalancing { get; set; }

    /// <summary>
    /// true if Auto Heal is enabled; otherwise, false.
    /// </summary>
    public bool? AutoHealEnabled { get; set; }

    /// <summary>
    /// Tracing options.
    /// </summary>
    public string? TracingOptions { get; set; }

    /// <summary>
    /// Virtual Network name.
    /// </summary>
    public string? VnetName { get; set; }

    /// <summary>
    /// Virtual Network Route All enabled. This causes all outbound traffic to have Virtual Network Security Groups and User Defined Routes applied.
    /// </summary>
    public bool? VnetRouteAllEnabled { get; set; }

    /// <summary>
    /// The number of private ports assigned to this app. These will be assigned dynamically on runtime.
    /// </summary>
    public int? VnetPrivatePortsCount { get; set; }

    /// <summary>
    /// Auto-swap slot name.
    /// </summary>
    public string? AutoSwapSlotName { get; set; }

    /// <summary>
    /// true to enable local MySQL; otherwise, false.
    /// </summary>
    public bool? LocalMySqlEnabled { get; set; }

    /// <summary>
    /// Managed Service Identity Id
    /// </summary>
    public int? ManagedServiceIdentityId { get; set; }

    /// <summary>
    /// Explicit Managed Service Identity Id
    /// </summary>
    public int? XManagedServiceIdentityId { get; set; }

    /// <summary>
    /// Identity to use for Key Vault Reference authentication.
    /// </summary>
    public string? KeyVaultReferenceIdentity { get; set; }

    /// <summary>
    /// IP security restrictions for main.
    /// </summary>
    public IList<IpSecurityRestriction>? IpSecurityRestrictions { get; set; }

    /// <summary>
    /// Default action for main access restriction if no rules are matched.
    /// </summary>
    public DefaultAction? IpSecurityRestrictionsDefaultAction { get; set; }

    /// <summary>
    /// IP security restrictions for scm.
    /// </summary>
    public IList<IpSecurityRestriction>? ScmIpSecurityRestrictions { get; set; }

    /// <summary>
    /// Default action for scm access restriction if no rules are matched.
    /// </summary>
    public DefaultAction? ScmIpSecurityRestrictionsDefaultAction { get; set; }

    /// <summary>
    /// IP security restrictions for scm to use main.
    /// </summary>
    public bool? ScmIpSecurityRestrictionsUseMain { get; set; }

    /// <summary>
    /// Http20Enabled: configures a web site to allow clients to connect over http2.0
    /// </summary>
    public bool? Http20Enabled { get; set; }

    /// <summary>
    /// MinTlsVersion: configures the minimum version of TLS required for SSL requests
    /// </summary>
    public SupportedTlsVersions? MinTlsVersion { get; set; }

    /// <summary>
    /// ScmMinTlsVersion: configures the minimum version of TLS required for SSL requests for SCM site
    /// </summary>
    public SupportedTlsVersions? ScmMinTlsVersion { get; set; }

    /// <summary>
    /// State of FTP / FTPS service
    /// </summary>
    public FtpsState? FtpsState { get; set; }

    /// <summary>
    /// Number of preWarmed instances.
    /// This setting only applies to the Consumption and Elastic Plans
    /// </summary>
    public int? PreWarmedInstanceCount { get; set; }

    /// <summary>
    /// Maximum number of workers that a site can scale out to.
    /// This setting only applies to the Consumption and Elastic Premium Plans
    /// </summary>
    public int? FunctionAppScaleLimit { get; set; }

    /// <summary>
    /// Maximum number of workers that a site can scale out to.
    /// This setting only applies to apps in plans where ElasticScaleEnabled is true
    /// </summary>
    public int? ElasticWebAppScaleLimit { get; set; }

    /// <summary>
    /// Health check path
    /// </summary>
    public string? HealthCheckPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether functions runtime scale monitoring is enabled. When enabled,
    /// the ScaleController will not monitor event sources directly, but will instead call to the
    /// runtime to get scale status.
    /// </summary>
    public bool? FunctionsRuntimeScaleMonitoringEnabled { get; set; }

    /// <summary>
    /// Sets the time zone a site uses for generating timestamps. Compatible with Linux and Windows App Service.
    /// Setting the WEBSITE_TIME_ZONE app setting takes precedence over this config.
    /// </summary>
    public string? WebsiteTimeZone { get; set; }

    /// <summary>
    /// Number of minimum instance count for a site
    /// This setting only applies to the Elastic Plans
    /// </summary>
    public int? MinimumElasticInstanceCount { get; set; }

    /// <summary>
    /// List of Azure Storage Accounts.
    /// </summary>
    public IDictionary<string, AzureStorageInfoValue>? AzureStorageAccounts { get; set; }

    /// <summary>
    /// Property to allow or block all public traffic.
    /// </summary>
    public string? PublicNetworkAccess { get; set; }
}

/// <summary>
/// SCM type.
/// </summary>
public enum ScmType
{
    /// <summary>
    /// None.
    /// </summary>
    None,

    /// <summary>
    /// Dropbox.
    /// </summary>
    Dropbox,

    /// <summary>
    /// Team Foundation Server.
    /// </summary>
    Tfs,

    /// <summary>
    /// Local Git.
    /// </summary>
    LocalGit,

    /// <summary>
    /// GitHub.
    /// </summary>
    GitHub,

    /// <summary>
    /// CodePlex Git.
    /// </summary>
    CodePlexGit,

    /// <summary>
    /// CodePlex Hg.
    /// </summary>
    CodePlexHg,

    /// <summary>
    /// Bitbucket Git.
    /// </summary>
    BitbucketGit,

    /// <summary>
    /// Bitbucket Hg.
    /// </summary>
    BitbucketHg,

    /// <summary>
    /// External Git.
    /// </summary>
    ExternalGit,

    /// <summary>
    /// External Hg.
    /// </summary>
    ExternalHg,

    /// <summary>
    /// OneDrive.
    /// </summary>
    OneDrive,

    /// <summary>
    /// Visual Studio Online.
    /// </summary>
    VSO,

    /// <summary>
    /// VSTS Release Management.
    /// </summary>
    VSTSRM
}

/// <summary>
/// Managed pipeline mode.
/// </summary>
public enum ManagedPipelineMode
{
    /// <summary>
    /// Integrated.
    /// </summary>
    Integrated,

    /// <summary>
    /// Classic.
    /// </summary>
    Classic
}

/// <summary>
/// Site load balancing mode.
/// </summary>
public enum SiteLoadBalancing
{
    /// <summary>
    /// Weighted round robin.
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// Least requests.
    /// </summary>
    LeastRequests,

    /// <summary>
    /// Least response time.
    /// </summary>
    LeastResponseTime,

    /// <summary>
    /// Weighted total traffic.
    /// </summary>
    WeightedTotalTraffic,

    /// <summary>
    /// Request hash.
    /// </summary>
    RequestHash,

    /// <summary>
    /// Per site round robin.
    /// </summary>
    PerSiteRoundRobin
}

/// <summary>
/// Default action.
/// </summary>
public enum DefaultAction
{
    /// <summary>
    /// Allow access.
    /// </summary>
    Allow,

    /// <summary>
    /// Deny access.
    /// </summary>
    Deny
}

/// <summary>
/// Supported TLS versions.
/// </summary>
public enum SupportedTlsVersions
{
    /// <summary>
    /// TLS 1.0
    /// </summary>
    V1_0,

    /// <summary>
    /// TLS 1.1
    /// </summary>
    V1_1,

    /// <summary>
    /// TLS 1.2
    /// </summary>
    V1_2
}

/// <summary>
/// FTPS state.
/// </summary>
public enum FtpsState
{
    /// <summary>
    /// All allowed.
    /// </summary>
    AllAllowed,

    /// <summary>
    /// FTPS only.
    /// </summary>
    FtpsOnly,

    /// <summary>
    /// Disabled.
    /// </summary>
    Disabled
}

/// <summary>
/// Virtual application in an app.
/// </summary>
public class VirtualApplication
{
    /// <summary>
    /// Virtual path.
    /// </summary>
    public string? VirtualPath { get; set; }

    /// <summary>
    /// Physical path.
    /// </summary>
    public string? PhysicalPath { get; set; }

    /// <summary>
    /// true if preloading is enabled; otherwise, false.
    /// </summary>
    public bool? PreloadEnabled { get; set; }

    /// <summary>
    /// Virtual directories for virtual application.
    /// </summary>
    public IList<VirtualDirectory>? VirtualDirectories { get; set; }
}

/// <summary>
/// Virtual directory for virtual application.
/// </summary>
public class VirtualDirectory
{
    /// <summary>
    /// Path to virtual application.
    /// </summary>
    public string? VirtualPath { get; set; }

    /// <summary>
    /// Physical path.
    /// </summary>
    public string? PhysicalPath { get; set; }
}

/// <summary>
/// Name-value pair.
/// </summary>
public class NameValuePair
{
    /// <summary>
    /// Pair name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Pair value.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Connection string information.
/// </summary>
public class ConnStringInfo
{
    /// <summary>
    /// Name of connection string.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Connection string value.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Type of database.
    /// </summary>
    public ConnectionStringType? Type { get; set; }
}

/// <summary>
/// Connection string type.
/// </summary>
public enum ConnectionStringType
{
    /// <summary>
    /// MySQL.
    /// </summary>
    MySql,

    /// <summary>
    /// SQL Server.
    /// </summary>
    SQLServer,

    /// <summary>
    /// SQL Azure.
    /// </summary>
    SQLAzure,

    /// <summary>
    /// Custom.
    /// </summary>
    Custom,

    /// <summary>
    /// Notification Hub.
    /// </summary>
    NotificationHub,

    /// <summary>
    /// Service Bus.
    /// </summary>
    ServiceBus,

    /// <summary>
    /// Event Hub.
    /// </summary>
    EventHub,

    /// <summary>
    /// API Hub.
    /// </summary>
    ApiHub,

    /// <summary>
    /// Document DB.
    /// </summary>
    DocDb,

    /// <summary>
    /// Redis Cache.
    /// </summary>
    RedisCache,

    /// <summary>
    /// PostgreSQL.
    /// </summary>
    PostgreSQL
}

/// <summary>
/// The IIS handler mappings used to define which handler processes HTTP requests with certain extension.
/// For example, it is used to configure php-cgi.exe process to handle all HTTP requests with *.php extension.
/// </summary>
public class HandlerMapping
{
    /// <summary>
    /// Requests with this extension will be handled using the specified FastCGI application.
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// The absolute path to the FastCGI application.
    /// </summary>
    public string? ScriptProcessor { get; set; }

    /// <summary>
    /// Command-line arguments to be passed to the script processor.
    /// </summary>
    public string? Arguments { get; set; }
}

/// <summary>
/// IP security restriction on an app.
/// </summary>
public class IpSecurityRestriction
{
    /// <summary>
    /// IP address the security restriction is valid for.
    /// It can be in form of pure ipv4 address (required SubnetMask property) or
    /// CIDR notation such as ipv4/mask (leading bit match). For CIDR,
    /// SubnetMask property must not be specified.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Subnet mask for the range of IP addresses the restriction is valid for.
    /// </summary>
    public string? SubnetMask { get; set; }

    /// <summary>
    /// Virtual network resource id
    /// </summary>
    public string? VnetSubnetResourceId { get; set; }

    /// <summary>
    /// (internal) Vnet traffic tag
    /// </summary>
    public int? VnetTrafficTag { get; set; }

    /// <summary>
    /// (internal) Subnet traffic tag
    /// </summary>
    public int? SubnetTrafficTag { get; set; }

    /// <summary>
    /// Allow or Deny access for this IP range.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Defines what this IP filter will be used for. This is to support IP filtering on proxies.
    /// </summary>
    public IpFilterTag? Tag { get; set; }

    /// <summary>
    /// Priority of IP restriction rule.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// IP restriction rule name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// IP restriction rule description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// IP restriction rule headers.
    /// </summary>
    public IDictionary<string, IList<string>>? Headers { get; set; }
}

/// <summary>
/// IP filter tag.
/// </summary>
public enum IpFilterTag
{
    /// <summary>
    /// Default.
    /// </summary>
    Default,

    /// <summary>
    /// XFF Proxy.
    /// </summary>
    XffProxy,

    /// <summary>
    /// Service Tag.
    /// </summary>
    ServiceTag
}

/// <summary>
/// Cloning information.
/// </summary>
public class CloningInfo
{
    /// <summary>
    /// Correlation ID of cloning operation. This ID ties multiple cloning operations
    /// together to use the same snapshot.
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// true to overwrite destination app; otherwise, false.
    /// </summary>
    public bool? Overwrite { get; set; }

    /// <summary>
    /// true to clone custom hostnames from source app; otherwise, false.
    /// </summary>
    public bool? CloneCustomHostNames { get; set; }

    /// <summary>
    /// true to clone source control from source app; otherwise, false.
    /// </summary>
    public bool? CloneSourceControl { get; set; }

    /// <summary>
    /// ARM resource ID of the source app. App resource ID is of the form
    /// /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName} for production slots and
    /// /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName}/slots/{slotName} for other slots.
    /// </summary>
    public string? SourceWebAppId { get; set; }

    /// <summary>
    /// Location of source app ex: West US or North Europe
    /// </summary>
    public string? SourceWebAppLocation { get; set; }

    /// <summary>
    /// App Service Environment.
    /// </summary>
    public string? HostingEnvironment { get; set; }

    /// <summary>
    /// Application setting overrides for cloned app. If specified, these settings override the settings cloned
    /// from source app. Otherwise, application settings from source app are retained.
    /// </summary>
    public IDictionary<string, string>? AppSettingsOverrides { get; set; }

    /// <summary>
    /// true to configure load balancing for source and destination app.
    /// </summary>
    public bool? ConfigureLoadBalancing { get; set; }

    /// <summary>
    /// ARM resource ID of the Traffic Manager profile to use, if it exists. Traffic Manager resource ID is of the form
    /// /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/trafficManagerProfiles/{profileName}.
    /// </summary>
    public string? TrafficManagerProfileId { get; set; }

    /// <summary>
    /// Name of Traffic Manager profile to create. This is only needed if Traffic Manager profile does not already exist.
    /// </summary>
    public string? TrafficManagerProfileName { get; set; }
}

/// <summary>
/// Slot swap status.
/// </summary>
public class SlotSwapStatus
{
    /// <summary>
    /// The time the last successful slot swap completed.
    /// </summary>
    public DateTime? TimestampUtc { get; set; }

    /// <summary>
    /// The source slot of the last swap operation.
    /// </summary>
    public string? SourceSlotName { get; set; }

    /// <summary>
    /// The destination slot of the last swap operation.
    /// </summary>
    public string? DestinationSlotName { get; set; }
}

/// <summary>
/// Specification for an App Service Environment to use for this resource.
/// </summary>
public class HostingEnvironmentProfile
{
    /// <summary>
    /// Resource ID of the App Service Environment.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Name of the App Service Environment.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Resource type of the App Service Environment.
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Managed service identity.
/// </summary>
public class ManagedServiceIdentity
{
    /// <summary>
    /// Type of managed service identity.
    /// </summary>
    public ManagedServiceIdentityType? Type { get; set; }

    /// <summary>
    /// Tenant of managed service identity.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Principal Id of managed service identity.
    /// </summary>
    public string? PrincipalId { get; set; }

    /// <summary>
    /// The list of user assigned identities associated with the resource. The user identity dictionary key references will be ARM resource ids in the form:
    /// '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{identityName}
    /// </summary>
    public IDictionary<string, UserAssignedIdentity>? UserAssignedIdentities { get; set; }
}

/// <summary>
/// Managed service identity type.
/// </summary>
public enum ManagedServiceIdentityType
{
    /// <summary>
    /// System assigned.
    /// </summary>
    SystemAssigned,

    /// <summary>
    /// User assigned.
    /// </summary>
    UserAssigned,

    /// <summary>
    /// System assigned and user assigned.
    /// </summary>
    SystemAssignedUserAssigned,

    /// <summary>
    /// None.
    /// </summary>
    None
}

/// <summary>
/// User assigned identity.
/// </summary>
public class UserAssignedIdentity
{
    /// <summary>
    /// Principal ID.
    /// </summary>
    public string? PrincipalId { get; set; }

    /// <summary>
    /// Client ID.
    /// </summary>
    public string? ClientId { get; set; }
}

/// <summary>
/// Extended location.
/// </summary>
public class ExtendedLocation
{
    /// <summary>
    /// Name of extended location.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Type of extended location.
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Azure storage information value for dictionary storage.
/// </summary>
public class AzureStorageInfoValue
{
    /// <summary>
    /// Type of storage.
    /// </summary>
    public AzureStorageType? Type { get; set; }

    /// <summary>
    /// Name of the storage account.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Name of the file share (container name, for Blob storage).
    /// </summary>
    public string? ShareName { get; set; }

    /// <summary>
    /// Access key for the storage account.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// Path to mount the storage within the site's runtime environment.
    /// </summary>
    public string? MountPath { get; set; }

    /// <summary>
    /// State of the storage account.
    /// </summary>
    public AzureStorageState? State { get; set; }
}

/// <summary>
/// Azure storage type.
/// </summary>
public enum AzureStorageType
{
    /// <summary>
    /// Azure Files.
    /// </summary>
    AzureFiles,

    /// <summary>
    /// Azure Blob.
    /// </summary>
    AzureBlob
}

/// <summary>
/// Azure storage state.
/// </summary>
public enum AzureStorageState
{
    /// <summary>
    /// OK.
    /// </summary>
    Ok,

    /// <summary>
    /// Invalid credentials.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// Invalid share.
    /// </summary>
    InvalidShare,

    /// <summary>
    /// Not validated.
    /// </summary>
    NotValidated
}

/// <summary>
/// Azure resource. This resource is tracked in Azure Resource Manager
/// </summary>
public class Resource
{
    /// <summary>
    /// Resource Id.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Resource Name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Kind of resource.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Resource Location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Resource type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Resource tags.
    /// </summary>
    public IDictionary<string, string>? Tags { get; set; }
}

