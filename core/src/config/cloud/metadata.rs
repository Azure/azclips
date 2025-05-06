use std::{
    collections::HashMap,
    fmt::{self, Display},
    str::FromStr,
};

use serde::{Deserialize, Serialize};

use super::AzCloud;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzCloudMetadata {
    #[serde(
        serialize_with = "AzCloud::serialize_az_cloud",
        deserialize_with = "AzCloud::deserialize_az_cloud"
    )]
    pub(crate) name: AzCloud,
    endpoints: HashMap<String, String>,
    suffixes: HashMap<String, String>,
}

impl AzCloudMetadata {
    pub fn forbid_telemetry(&self) -> bool {
        match &self.name {
            // Add names of clouds that don't allow telemetry data collection here such as some air-gapped clouds.
            AzCloud::Custom(custom_name) => {
                custom_name.eq_ignore_ascii_case("USSec")
                    || custom_name.eq_ignore_ascii_case("USNat")
            }
            _ => false,
        }
    }
}

#[derive(Debug, Hash, Eq, PartialEq)]
pub(crate) enum AzCloudStandardEndpoint {
    /// The Active Directory login endpoint
    ActiveDirectory,
    /// The Active Directory resource ID for data lake services.
    ActiveDirectoryDataLakeResourceId,
    /// The Active Directory resource ID.
    ActiveDirectoryGraphResourceId,
    /// The resource ID to obtain AD tokens for.
    ActiveDirectoryResourceId,
    /// The resource ID of the App Insights resource.
    AppInsightsResourceId,
    /// The resource ID of the App Insights telemetry channel.
    AppInsightsTelemetryChannelResourceId,
    /// The resource ID of the attestation resource.
    AttestationResourceId,
    /// The resource ID of the azmirror storage account.
    AzmirrorStorageAccountResourceId,
    /// The resource ID of the batch resource.
    BatchReesourceId,
    /// The template gallery endpoint.
    Gallery,
    /// The resource ID of the log analytics resource.
    LogAnalyticsResourceId,
    /// The management service endpoint.
    Management,
    /// The resource ID of the media resource.
    MediaResourceId,
    /// The resource ID of the Microsoft Graph resource.
    MicrosoftGraphResourceId,
    /// The resource ID of the ossrdbms resource.
    OssrdbmsResourceId,
    /// The portal endpoint.
    Portal,
    /// The resource management endpoint.
    ResourceManager,
    /// The sql server management endpoint.
    SqlManagement,
    /// The resource ID of the synapse analytics resource.
    SynapseAnalyticsResourceId,
    /// The uri of the document which caches commonly used virtual machine images.
    VmImageAliasDoc,
}

#[derive(Debug, Hash, Eq, PartialEq)]
pub(crate) enum AzCloudStandardSuffix {
    /// The Azure Container Registry login server suffix.
    AcrLoginServerEndpoint,
    /// The attestation endpoint.
    AttestationEndpoint,
    /// The Data Lake analytics job and catalog service dns suffix.
    AzureDatalakeAnalyticsCatalogAndJobEndpoint,
    /// The Data Lake store filesystem service dns suffix.
    AzureDatalakeStoreFileSystemEndpoint,
    /// The Key Vault service dns suffix.
    KeyVaultDns,
    /// The mariadb server endpoint.
    MariadbServerEndpoint,
    /// The mhsm dns.
    MhsmDns,
    /// The mysql server endpoint.
    MysqlServerEndpoint,
    /// The postgres server endpoint.
    PostgresServerEndpoint,
    /// The dns suffix for sql servers.
    SqlServerHostname,
    /// The endpoint suffix for storage accounts.
    StorageEndpoint,
    /// The storage sync endpoint.
    StorageSyncEndpoint,
    /// The synapse analytics endpoint.
    SynapseAnalyticsEndpoint,
}

struct EndpointMap(HashMap<String, String>);

struct SuffixMap(HashMap<String, String>);
