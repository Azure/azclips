#[derive(Clone, Eq, PartialEq, Hash)]
pub struct AudienceName {
    inner: Repr,
}

#[derive(Debug, Clone, Eq, PartialEq, Hash)]
enum Repr {
    Standard(StandardAudience),
    Custom(String),
}

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
        enum StandardAudience {
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

standard_audiences! {

    /// The Active Directory resource identifier for data lake services.
    (ActiveDirectoryDataLake, ACTIVE_DIRECTORY_DATALAKE, b"activeDirectoryDataLake", b"activeDirectoryDataLake");
    /// The resource identifier of the App Insights resource.
    (AppInsights, APP_INSIGHTSD, b"appInsights", b"appInsightsResourceId");
    /// The resource identifier of the App Insights telemetry channel.
    (AppInsightsTelemetryChannel, APP_INSIGHTS_TELEMETRY_CHANNEL, b"appInsightsTelemetryChannel", b"appInsightsTelemetryChannelResourceId");
    /// The resource identifier of the attestation resource.
    (Attestation, ATTESTATION, b"attestation", b"attestationResourceId");
    /// The resource identifier to obtain login tokens for.
    (Authentication, AUTHENTICATION, b"authentication", b"authentication.audiences[0]");
    /// The resource identifier of the azmirror storage account.
    (AzmirrorStorageAccount, AZMIRROR_STORAGE_ACCOUNT, b"azmirrorStorageAccount", b"azmirrorStorageAccountResourceId");
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
    (ResourceManager, RESOURCE_MANAGER, b"resourceManager", b"resourceManager");
    /// The resource identifier of the synapse analytics resource.
    (SynapseAnalytics, SYNAPSE_ANALYTICS, b"synapseAnalytics", b"synapseAnalyticsResourceId");


}
