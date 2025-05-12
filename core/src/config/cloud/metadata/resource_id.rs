use once_cell::sync::Lazy;
use serde::ser::SerializeMap;
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::collections::HashMap;
use std::convert::Infallible;
use std::str::FromStr;
use std::sync::Arc;
use std::sync::RwLock;

use crate::pb::command::{cmd_arg, CmdArg, CmdArgHelp, CmdStringArg};
use crate::AzCloud;

#[derive(Clone, Eq, PartialEq, Hash, Debug)]
pub struct ResourceIdName {
    pub(super) inner: Repr,
}

#[derive(Debug, Clone, Eq, PartialEq, Hash)]
pub(super) enum Repr {
    Standard(StandardResourceId),
    Custom(String),
}
#[derive(Debug, Default, Clone)]
pub(crate) struct ResourceIdMap(pub(super) Arc<RwLock<HashMap<ResourceIdName, String>>>);

/// This macro defines standard resource ids for Azure cloud services.
///
/// # Parameters
///
/// * `$konst` - The constant identifier for the resource id
/// * `$upcase` - The uppercase constant name
/// * `$name_bytes` - The byte string literal for the resource id name
/// * `$kebab_bytes` - The byte string literal for the kebab case resource id name
/// * `$var_bytes` - The byte string literal for the index path to retrieve the resource id from [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
///
/// # Examples
///
/// ```
/// standard_resource_ids! {
///     (Authentication, AUTHENTICATION, b"authentication", b"authentication", b"authentication.resource_ids[0]");
/// }
/// ```
macro_rules! standard_resource_ids {
    (
        $(
            $(#[$docs:meta])*
            ($konst:ident, $upcase:ident, $name_bytes:literal, $kebab_bytes:literal, $var_bytes:literal);
        )+
    ) => {
        #[derive(Debug, Clone, Copy, Eq, PartialEq, Hash)]
        pub(crate) enum StandardResourceId {
            $(
                $konst,
            )+
        }

        $(
            $(#[$docs])*
            pub const $upcase: ResourceIdName = ResourceIdName {
                inner: Repr::Standard(StandardResourceId::$konst),
            };
        )+

        impl StandardResourceId {
            #[inline]
            fn as_str(&self) -> &'static str {
                match *self {
                    $(
                        Self::$konst => unsafe { std::str::from_utf8_unchecked( $name_bytes ) },
                    )+
                }
            }

            #[inline]
            fn var_str(&self) -> &'static str {
                match *self {
                    $(
                        Self::$konst => unsafe { std::str::from_utf8_unchecked( $var_bytes) },
                    )+
                }
            }

            #[inline]
            const fn from_bytes(name: &[u8]) -> Option<Self> {
                match name {
                    $(
                        $name_bytes => Some(Self::$konst),
                    )+
                    _ => None,
                }
            }

            #[inline]
            const fn from_var_bytes(var_bytes: &[u8]) -> Option<Self> {
                match var_bytes {
                    $(
                        $var_bytes => Some(Self::$konst),
                    )+
                    _ => None,
                }
            }

            pub fn list_args(nullable: bool) -> Vec<CmdArg> {
                vec![
                    $(
                        CmdArg {
                            name: unsafe { std::str::from_utf8_unchecked($name_bytes) }.to_string(),
                            help: Some(CmdArgHelp {
                                short: format!("{} service.", unsafe { std::str::from_utf8_unchecked($name_bytes) }),
                                long: None,
                            }),
                            group: "Standard Resource Id".to_string(),
                            options: vec![unsafe { std::str::from_utf8_unchecked($kebab_bytes) }.to_string()],
                            required: false,
                            nullable,
                            arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                                ..Default::default()
                            })),
                            ..Default::default()
                        },
                    )+
                ]
            }
        }
    };
}

impl ResourceIdName {
    pub fn as_str(&self) -> &str {
        match &self.inner {
            Repr::Standard(name) => name.as_str(),
            Repr::Custom(name) => name,
        }
    }

    pub(crate) fn get_var_str(&self) -> Option<&str> {
        match &self.inner {
            Repr::Standard(resource) => Some(resource.var_str()),
            Repr::Custom(_) => None,
        }
    }
}

impl FromStr for ResourceIdName {
    type Err = Infallible;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        if let Some(resource) = StandardResourceId::from_bytes(s.to_lowercase().as_bytes()) {
            Ok(ResourceIdName {
                inner: Repr::Standard(resource),
            })
        } else {
            Ok(ResourceIdName {
                inner: Repr::Custom(s.to_string()),
            })
        }
    }
}

impl ResourceIdMap {
    pub fn get(&self, name: &ResourceIdName) -> Option<String> {
        self.0.read().unwrap().get(name).map(|s| s.clone())
    }

    pub fn insert(&mut self, name: ResourceIdName, value: &str) -> Option<String> {
        self.0.write().unwrap().insert(name, value.to_string())
    }

    pub fn remove(&mut self, name: &ResourceIdName) -> Option<String> {
        self.0.write().unwrap().remove(name)
    }

    pub fn from<'a>(iter: impl IntoIterator<Item = (&'a ResourceIdName, &'a str)>) -> Self {
        let map = iter
            .into_iter()
            .map(|(k, v)| (k.clone(), v.to_string()))
            .collect();
        Self(Arc::new(RwLock::new(map)))
    }
}

impl Serialize for ResourceIdMap {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let map = self.0.read().unwrap();
        let mut serializer = serializer.serialize_map(Some(map.len()))?;
        for (k, v) in map.iter() {
            serializer.serialize_entry(k.as_str(), v)?;
        }
        serializer.end()
    }
}

impl<'de> Deserialize<'de> for ResourceIdName {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        Ok(ResourceIdName::from_str(&s).unwrap())
    }
}

impl<'de> Deserialize<'de> for ResourceIdMap {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let map = HashMap::<ResourceIdName, String>::deserialize(deserializer)?;
        Ok(ResourceIdMap(Arc::new(RwLock::new(map))))
    }
}

standard_resource_ids! {
    /// The Active Directory resource identifier for data lake services.
    (ActiveDirectoryDataLake, ACTIVE_DIRECTORY_DATALAKE, b"activeDirectoryDataLake", b"active-directory-data-lake", b"activeDirectoryDataLake");
    /// The resource identifier of the App Insights resource.
    (AppInsights, APP_INSIGHTS, b"appInsights", b"app-insights", b"appInsightsResourceId");
    /// The resource identifier of the App Insights telemetry channel.
    (AppInsightsTelemetryChannel, APP_INSIGHTS_TELEMETRY_CHANNEL, b"appInsightsTelemetryChannel", b"app-insights-telemetry-channel", b"appInsightsTelemetryChannelResourceId");
    /// The resource identifier of the attestation resource.
    (Attestation, ATTESTATION, b"attestation", b"attestation", b"attestationResourceId");
    /// The resource identifier to obtain login tokens for.
    (Authentication, AUTHENTICATION, b"authentication", b"authentication", b"authentication.resource_ids[0]");
    /// The resource identifier of the Azure Batch service that is the recipient of the requested token.
    (Batch, BATCH, b"batch", b"batch", b"batch");
    /// The Active Directory resource identifier.
    (Graph, GRAPH, b"graph", b"graph", b"graphResourceId");
    /// The resource identifier of the log analytics resource.
    (LogAnalytics, LOG_ANALYTICS, b"logAnalytics", b"log-analytics", b"logAnalyticsResourceId");
    /// The resource identifier of the media resource.
    (Media, MEDIA, b"media", b"media", b"media");
    /// The resource identifier of the Microsoft Graph resource.
    (MicrosoftGraph, MICROSOFT_GRAPH, b"microsoftGraph", b"microsoft-graph", b"microsoftGraphResourceId");
    /// The resource identifier of the ossrdbms resource.
    (Ossrdbms, OSSRDBMS, b"ossrdbms", b"ossrdbms", b"ossrdbmsResourceId");
    /// The resource identifier of the resource manager resource.
    (ResourceManager, RESOURCE_MANAGER, b"resourceManager", b"resource-manager", b"authentication.resource_ids[0]");
    /// The resource identifier of the synapse analytics resource.
    (SynapseAnalytics, SYNAPSE_ANALYTICS, b"synapseAnalytics", b"synapse-analytics", b"synapseAnalyticsResourceId");
}

/// This map contains the built-in resource_id maps for the Azure clouds.
pub(super) static BUILTIN_RESOURCE_ID_MAPS: Lazy<HashMap<AzCloud, ResourceIdMap>> =
    Lazy::new(|| {
        HashMap::from([
            (
                AzCloud::AzureCloud,
                ResourceIdMap::from([
                    (&ACTIVE_DIRECTORY_DATALAKE, "https://datalake.azure.net/"),
                    (&APP_INSIGHTS, "https://api.applicationinsights.io"),
                    (
                        &APP_INSIGHTS_TELEMETRY_CHANNEL,
                        "https://dc.applicationinsights.azure.com/v2/track",
                    ),
                    (&ATTESTATION, "https://attest.azure.net"),
                    (&AUTHENTICATION, "https://management.core.windows.net/"),
                    (&BATCH, "https://batch.core.windows.net/"),
                    (&GRAPH, "https://graph.windows.net/"),
                    (&LOG_ANALYTICS, "https://api.loganalytics.io"),
                    (&MEDIA, "https://rest.media.azure.net"),
                    (&MICROSOFT_GRAPH, "https://graph.microsoft.com/"),
                    (&OSSRDBMS, "https://ossrdbms-aad.database.windows.net"),
                    (&RESOURCE_MANAGER, "https://management.core.windows.net/"), // or use https://management.azure.com/
                    (&SYNAPSE_ANALYTICS, "https://dev.azuresynapse.net"),
                ]),
            ),
            (
                AzCloud::AzureChinaCloud,
                ResourceIdMap::from([
                    (&APP_INSIGHTS, "https://api.applicationinsights.azure.cn"),
                    (
                        &APP_INSIGHTS_TELEMETRY_CHANNEL,
                        "https://dc.applicationinsights.azure.cn/v2/track",
                    ),
                    (&AUTHENTICATION, "https://management.core.chinacloudapi.cn/"),
                    (&BATCH, "https://batch.chinacloudapi.cn/"),
                    (&GRAPH, "https://graph.chinacloudapi.cn/"),
                    (&LOG_ANALYTICS, "https://api.loganalytics.azure.cn"),
                    (&MEDIA, "https://rest.media.chinacloudapi.cn"),
                    (&MICROSOFT_GRAPH, "https://microsoftgraph.chinacloudapi.cn"),
                    (&OSSRDBMS, "https://ossrdbms-aad.database.chinacloudapi.cn"),
                    (
                        &RESOURCE_MANAGER,
                        "https://management.core.chinacloudapi.cn/",
                    ), // or use https://management.chinacloudapi.cn/
                    (&SYNAPSE_ANALYTICS, "https://dev.azuresynapse.azure.cn"),
                ]),
            ),
            (
                AzCloud::AzureUSGovernment,
                ResourceIdMap::from([
                    (&APP_INSIGHTS, "https://api.applicationinsights.us"),
                    (
                        &APP_INSIGHTS_TELEMETRY_CHANNEL,
                        "https://dc.applicationinsights.us/v2/track",
                    ),
                    (
                        &AUTHENTICATION,
                        "https://management.core.usgovcloudapi.net/",
                    ),
                    (&BATCH, "https://batch.core.usgovcloudapi.net/"),
                    (&GRAPH, "https://graph.windows.net/"),
                    (&LOG_ANALYTICS, "https://api.loganalytics.us"),
                    (&MEDIA, "https://rest.media.usgovcloudapi.net"),
                    (&MICROSOFT_GRAPH, "https://graph.microsoft.us/"),
                    (&OSSRDBMS, "https://ossrdbms-aad.database.usgovcloudapi.net"),
                    (
                        &RESOURCE_MANAGER,
                        "https://management.core.usgovcloudapi.net/",
                    ), // or use https://management.usgovcloudapi.net/
                    (
                        &SYNAPSE_ANALYTICS,
                        "https://dev.azuresynapse.usgovcloudapi.net",
                    ),
                ]),
            ),
            (
                AzCloud::AzureGermanCloud,
                ResourceIdMap::from([
                    (&AUTHENTICATION, "https://management.core.cloudapi.de/"),
                    (&BATCH, "https://batch.cloudapi.de/"),
                    (&GRAPH, "https://graph.cloudapi.de/"),
                    (&MEDIA, "https://rest.media.cloudapi.de"),
                    (&MICROSOFT_GRAPH, "https://graph.microsoft.de"),
                    (&OSSRDBMS, "https://ossrdbms-aad.database.cloudapi.de"),
                    (&RESOURCE_MANAGER, "https://management.core.cloudapi.de/"), // or use https://management.microsoftazure.de/
                ]),
            ),
        ])
    });
