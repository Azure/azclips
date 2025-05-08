use once_cell::sync::Lazy;
use serde::de::{MapAccess, Visitor};
use serde::ser::SerializeMap;
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::collections::HashMap;
use std::fmt;

use crate::AzCloud;

#[derive(Clone, Eq, PartialEq, Hash, Debug)]
pub struct AudienceName {
    pub(super) inner: Repr,
}

#[derive(Debug, Clone, Eq, PartialEq, Hash)]
pub(super) enum Repr {
    Standard(StandardAudience),
    Custom(String),
}
#[derive(Debug, Default)]
pub(crate) struct AudienceMap(HashMap<AudienceName, String>);

/// This macro defines standard audiences for Azure cloud services.
///
/// # Parameters
///
/// * `$konst` - The constant identifier for the audience
/// * `$upcase` - The uppercase constant name
/// * `$name_bytes` - The byte string literal for the audience name
/// * `$var_bytes` - The byte string literal for the index path to retrieve the value from [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
///
/// # Examples
///
/// ```
/// standard_audiences! {
///     (ActiveDirectoryDataLake, ACTIVE_DIRECTORY_DATALAKE, b"activeDirectoryDataLake", b"activeDirectoryDataLake");
/// }
/// ```
macro_rules! standard_audiences {
    (
        $(
            $(#[$docs:meta])*
            ($konst:ident, $upcase:ident, $name_bytes:literal, $var_bytes:literal);
        )+
    ) => {
        #[derive(Debug, Clone, Copy, Eq, PartialEq, Hash)]
        pub(super) enum StandardAudience {
            $(
                $konst,
            )+
        }

        $(
            $(#[$docs])*
            pub const $upcase: AudienceName = AudienceName {
                inner: Repr::Standard(StandardAudience::$konst),
            };
        )+

        impl StandardAudience {
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
        }
    };
}

impl AudienceName {
    pub fn as_str(&self) -> &str {
        match &self.inner {
            Repr::Standard(audience) => audience.as_str(),
            Repr::Custom(name) => name,
        }
    }

    pub fn from_str(name: &str) -> Self {
        if let Some(audience) = StandardAudience::from_bytes(name.as_bytes()) {
            Self {
                inner: Repr::Standard(audience),
            }
        } else {
            Self {
                inner: Repr::Custom(name.to_string()),
            }
        }
    }
}

impl AudienceMap {
    pub fn get(&self, audience: &AudienceName) -> Option<&str> {
        self.0.get(audience).map(|s| s.as_str())
    }

    pub fn insert(&mut self, audience: AudienceName, value: &str) -> Option<String> {
        self.0.insert(audience, value.to_string())
    }

    pub fn from<'a>(iter: impl IntoIterator<Item = (&'a AudienceName, &'a str)>) -> Self {
        Self(
            iter.into_iter()
                .map(|(k, v)| (k.clone(), v.to_string()))
                .collect(),
        )
    }
}

impl Serialize for AudienceMap {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut map = serializer.serialize_map(Some(self.0.len()))?;
        for (k, v) in &self.0 {
            map.serialize_entry(k.as_str(), v)?;
        }
        map.end()
    }
}

impl<'de> Deserialize<'de> for AudienceMap {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        struct AudienceMapVisitor;

        impl<'de> Visitor<'de> for AudienceMapVisitor {
            type Value = AudienceMap;

            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("a map of audience names to strings")
            }

            fn visit_map<M>(self, mut access: M) -> Result<Self::Value, M::Error>
            where
                M: MapAccess<'de>,
            {
                let mut map = HashMap::with_capacity(access.size_hint().unwrap_or(0));
                while let Some((key, value)) = access.next_entry::<String, String>()? {
                    map.insert(AudienceName::from_str(&key), value);
                }
                Ok(AudienceMap(map))
            }
        }

        deserializer.deserialize_map(AudienceMapVisitor)
    }
}

standard_audiences! {

    /// The Active Directory resource identifier for data lake services.
    (ActiveDirectoryDataLake, ACTIVE_DIRECTORY_DATALAKE, b"activeDirectoryDataLake", b"activeDirectoryDataLake");
    /// The resource identifier of the App Insights resource.
    (AppInsights, APP_INSIGHTS, b"appInsights", b"appInsightsResourceId");
    /// The resource identifier of the App Insights telemetry channel.
    (AppInsightsTelemetryChannel, APP_INSIGHTS_TELEMETRY_CHANNEL, b"appInsightsTelemetryChannel", b"appInsightsTelemetryChannelResourceId");
    /// The resource identifier of the attestation resource.
    (Attestation, ATTESTATION, b"attestation", b"attestationResourceId");
    /// The resource identifier to obtain login tokens for.
    (Authentication, AUTHENTICATION, b"authentication", b"authentication.audiences[0]");
    // /// The resource identifier of the azmirror storage account.
    // (AzmirrorStorageAccount, AZMIRROR_STORAGE_ACCOUNT, b"azmirrorStorageAccount", b"azmirrorStorageAccountResourceId");  // Not found in metadata
    /// The resource identifier of the Azure Batch service that is the recipient of the requested token.
    (Batch, BATCH, b"batch", b"batch");
    /// The Active Directory resource identifier.
    (Graph, GRAPH, b"graph", b"graphAudience");
    /// The resource identifier of the log analytics resource.
    (LogAnalytics, LOG_ANALYTICS, b"logAnalytics", b"logAnalyticsResourceId");
    /// The resource identifier of the media resource.
    (Media, MEDIA, b"media", b"media");
    /// The resource identifier of the Microsoft Graph resource.
    (MicrosoftGraph, MICROSOFT_GRAPH, b"microsoftGraph", b"microsoftGraphResourceId");
    /// The resource identifier of the ossrdbms resource.
    (Ossrdbms, OSSRDBMS, b"ossrdbms", b"ossrdbmsResourceId");
    /// The resource identifier of the resource manager resource.
    (ResourceManager, RESOURCE_MANAGER, b"resourceManager", b"authentication.audiences[0]");  // or use resourceManager
    /// The resource identifier of the synapse analytics resource.
    (SynapseAnalytics, SYNAPSE_ANALYTICS, b"synapseAnalytics", b"synapseAnalyticsResourceId");
}

/// This map contains the built-in audience maps for the Azure clouds.
pub(super) static BUILTIN_AUDIENCE_MAPS: Lazy<HashMap<AzCloud, AudienceMap>> = Lazy::new(|| {
    HashMap::from([
        (
            AzCloud::AzureCloud,
            AudienceMap::from([
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
            AudienceMap::from([
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
            AudienceMap::from([
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
            AudienceMap::from([
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
