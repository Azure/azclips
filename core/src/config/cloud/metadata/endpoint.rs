use once_cell::sync::Lazy;
use serde::ser::SerializeMap;
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::collections::HashMap;
use std::convert::Infallible;
use std::str::FromStr;
use std::sync::Arc;
use std::sync::RwLock;

use super::AzCloud;
use crate::pb::{crs_arg, CrsArg, CrsArgHelp, CrsStringArg};

#[derive(Clone, Eq, PartialEq, Hash, Debug)]
pub struct EndpointName {
    inner: Repr,
}

#[derive(Debug, Default, Clone)]
pub(crate) struct EndpointMap(pub(super) Arc<RwLock<HashMap<EndpointName, String>>>);

#[derive(Debug, Clone, Eq, PartialEq, Hash)]
enum Repr {
    Endpoint(StandardEndpoint),
    Suffix(StandardSuffix),
    Custom(String),
}

/// This macro defines standard endpoints for Azure cloud services.
///
/// # Parameters
///
/// * `$konst` - The constant identifier for the endpoint
/// * `$upcase` - The uppercase constant name
/// * `$name_bytes` - The byte string literal for the endpoint name
/// * `$kebab_bytes` - The byte string literal for the kebab case endpoint name
/// * `$var_bytes` - The byte string literal for the index path to retrieve the value from [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
///
/// # Examples
///
/// standard_endpoints! {
///     (Authentication, AUTHENTICATION, b"authentication", b"authentication", b"authentication.loginEndpoint");
/// }
///
macro_rules! standard_endpoints {
        (
            $(
                $(#[$docs:meta])*
                ($konst:ident, $upcase:ident, $name_bytes:literal, $kebab_bytes:literal, $var_bytes:literal);
            )+
        ) => {
            #[derive(Debug, Clone, Copy, Eq, PartialEq, Hash)]
            pub(crate) enum StandardEndpoint {
                $(
                    $konst,
                )+
            }

            $(
                $(#[$docs])*
                pub const $upcase: EndpointName = EndpointName {
                    inner: Repr::Endpoint(StandardEndpoint::$konst),
                };
            )+

            /// The Standard Endpoint are those can defined in the [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
            impl StandardEndpoint {
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
                            Self::$konst => unsafe { std::str::from_utf8_unchecked( $var_bytes ) },
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

                // #[inline]
                // const fn from_var_bytes(var_bytes: &[u8]) -> Option<Self> {
                //     match var_bytes {
                //         $(
                //             $var_bytes => Some(Self::$konst),
                //         )+
                //         _ => None,
                //     }
                // }

                pub fn list_args(nullable: bool) -> Vec<CrsArg> {
                    vec![
                        $(
                            CrsArg {
                                name: unsafe { std::str::from_utf8_unchecked($name_bytes) }.to_string(),
                                help: Some(CrsArgHelp {
                                    short: format!("{} service.", unsafe { std::str::from_utf8_unchecked($name_bytes) }),
                                    long: None,
                                }),
                                group: "Endpoint Url".to_string(),
                                options: vec![unsafe { std::str::from_utf8_unchecked($kebab_bytes) }.to_string()],
                                required: false,
                                nullable,
                                arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
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

/// This macro defines standard suffixes for Azure cloud services.
///
/// # Parameters
///
/// * `$konst` - The constant identifier for the suffix
/// * `$upcase` - The uppercase constant name
/// * `$name_bytes` - The byte string literal for the suffix name
/// * `$kebab_bytes` - The byte string literal for the kebab case suffix name
/// * `$var_bytes` - The byte string literal for the index path to retrieve the value from [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
///
/// # Examples
///
/// standard_suffixes! {
///     (AcrLoginServer, ACR_LOGIN_SERVER, b"acrLoginServer", b"acr-login-server", b"suffixes.acrLoginServer");
/// }
///
macro_rules! standard_suffixes {
        (
            $(
                $(#[$docs:meta])*
                ($konst:ident, $upcase:ident, $name_bytes:literal, $kebab_bytes:literal, $var_bytes:literal);
            )+
        ) => {
            #[derive(Debug, Clone, Copy, Eq, PartialEq, Hash)]
            pub(crate) enum StandardSuffix {
                $(
                    $konst,
                )+
            }

            $(
                $(#[$docs])*
                pub const $upcase: EndpointName = EndpointName {
                    inner: Repr::Suffix(StandardSuffix::$konst),
                };
            )+

            /// The Standard Suffix are those defined in the [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
            impl StandardSuffix {
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
                            Self::$konst => unsafe { std::str::from_utf8_unchecked( $var_bytes ) },
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

                // #[inline]
                // const fn from_var_bytes(var_bytes: &[u8]) -> Option<Self> {
                //     match var_bytes {
                //         $(
                //             $var_bytes => Some(Self::$konst),
                //         )+
                //         _ => None,
                //     }
                // }

                pub fn list_args(nullable: bool) -> Vec<CrsArg> {
                    vec![
                        $(
                            CrsArg {
                                name: unsafe { std::str::from_utf8_unchecked($name_bytes) }.to_string(),
                                help: Some(CrsArgHelp {
                                    short: format!("{} service.", unsafe { std::str::from_utf8_unchecked($name_bytes) }),
                                    long: None,
                                }),
                                group: "Endpoint Suffix".to_string(),
                                options: vec![unsafe { std::str::from_utf8_unchecked($kebab_bytes) }.to_string()],
                                required: false,
                                nullable,
                                arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
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

impl EndpointName {
    pub fn as_str(&self) -> &str {
        match &self.inner {
            Repr::Endpoint(endpoint) => endpoint.as_str(),
            Repr::Suffix(suffix) => suffix.as_str(),
            Repr::Custom(name) => name,
        }
    }

    pub(crate) fn get_var_str(&self) -> Option<&str> {
        match &self.inner {
            Repr::Endpoint(endpoint) => Some(endpoint.var_str()),
            Repr::Suffix(suffix) => Some(suffix.var_str()),
            Repr::Custom(_) => None,
        }
    }
}

impl FromStr for EndpointName {
    type Err = Infallible;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        if let Some(endpoint) = StandardEndpoint::from_bytes(s.to_lowercase().as_bytes()) {
            Ok(Self {
                inner: Repr::Endpoint(endpoint),
            })
        } else if let Some(suffix) = StandardSuffix::from_bytes(s.to_lowercase().as_bytes()) {
            Ok(Self {
                inner: Repr::Suffix(suffix),
            })
        } else {
            Ok(Self {
                inner: Repr::Custom(s.to_string()),
            })
        }
    }
}

impl EndpointMap {
    pub fn get(&self, name: &EndpointName) -> Option<String> {
        self.0.read().unwrap().get(name).cloned()
    }

    pub fn insert(&mut self, name: EndpointName, value: &str) -> Option<String> {
        self.0.write().unwrap().insert(name, value.to_string())
    }

    pub fn remove(&mut self, name: &EndpointName) -> Option<String> {
        self.0.write().unwrap().remove(name)
    }

    pub fn from<'a>(iter: impl IntoIterator<Item = (&'a EndpointName, &'a str)>) -> Self {
        let map = iter
            .into_iter()
            .map(|(k, v)| (k.clone(), v.to_string()))
            .collect();
        Self(Arc::new(RwLock::new(map)))
    }
}

impl Serialize for EndpointMap {
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

impl<'de> Deserialize<'de> for EndpointName {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        Ok(EndpointName::from_str(&s).unwrap())
    }
}

impl<'de> Deserialize<'de> for EndpointMap {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let map = HashMap::<EndpointName, String>::deserialize(deserializer)?;
        Ok(EndpointMap(Arc::new(RwLock::new(map))))
    }
}

standard_endpoints! {
    /// The Active Directory data lake endpoint.
    (ActiveDirectoryDataLake, ACTIVE_DIRECTORY_DATALAKE, b"activeDirectoryDataLake", b"active-directory-data-lake", b"activeDirectoryDataLake");
    /// The Authentication login endpoint
    (Authentication, AUTHENTICATION, b"authentication", b"authentication", b"authentication.loginEndpoint");
    /// The Azure Batch endpoint.
    (Batch, BATCH, b"batch", b"batch", b"batch");
    /// The template gallery endpoint.
    (Gallery, GALLERY, b"gallery", b"gallery", b"gallery");
    /// The Active Directory resource identifier.
    (Graph, GRAPH, b"graph", b"graph", b"graph");
    /// The media endpoint.
    (Media, MEDIA, b"media", b"media", b"media");
    /// The portal endpoint.
    (Portal, PORTAL, b"portal", b"portal", b"portal");
    /// The resource management endpoint.
    (ResourceManager, RESOURCE_MANAGER, b"resourceManager", b"resource-manager", b"resourceManager");
    /// The sql server management endpoint.
    (SqlManagement, SQL_MANAGEMENT, b"sqlManagement", b"sql-management", b"sqlManagement");
    /// The uri of the document which caches commonly used virtual machine images.
    (VmImageAliasDoc, VM_IMAGE_ALIAS_DOC, b"vmImageAliasDoc", b"vm-image-alias-doc", b"vmImageAliasDoc");
}

standard_suffixes! {
    /// The Azure Container Registry login server suffix
    (AcrLoginServer, ACR_LOGIN_SERVER, b"acrLoginServer", b"acr-login-server", b"suffixes.acrLoginServer");
    /// The attestation endpoint.
    (Attestation, ATTESTATION, b"attestation", b"attestation", b"suffixes.attestationEndpoint");
    /// The Data Lake analytics job and catalog service dns suffix.
    (AzureDatalakeAnalyticsCatalogAndJob, AZURE_DATALAKE_ANALYTICS_CATALOG_AND_JOB, b"azureDatalakeAnalyticsCatalogAndJob", b"azure-datalake-analytics-catalog-and-job", b"suffixes.azureDataLakeAnalyticsCatalogAndJob");
    /// The Data Lake store filesystem service dns suffix.
    (AzureDatalakeStoreFileSystem, AZURE_DATALAKE_STORE_FILE_SYSTEM, b"azureDatalakeStoreFileSystem", b"azure-datalake-store-file-system", b"suffixes.azureDataLakeStoreFileSystem");
    /// The Azure Front Door service dns suffix.
    (AzureFrontDoor, AZURE_FRONT_DOOR, b"azureFrontDoor", b"azure-front-door", b"suffixes.azureFrontDoorEndpointSuffix");
    /// The Key Vault service dns suffix.
    (KeyVaultDns, KEY_VAULT_DNS, b"keyVaultDns", b"key-vault-dns", b"suffixes.keyVaultDns");
    /// The mariadb server endpoint.
    (MariadbServer, MARIADB_SERVER, b"mariadbServer", b"mariadb-server", b"suffixes.mariadbServerEndpoint");
    /// The mhsm dns.
    (MhsmDns, MHSM_DNS, b"mhsmDns", b"mhsm-dns", b"suffixes.mhsmDns");
    /// The mysql server endpoint.
    (MysqlServer, MYSQL_SERVER, b"mysqlServer", b"mysql-server", b"suffixes.mysqlServerEndpoint");
    /// The postgres server endpoint.
    (PostgresqlServer, POSTGRESQL_SERVER, b"postgresqlServer", b"postgresql-server", b"suffixes.postgresqlServerEndpoint");
    /// The dns suffix for sql servers.
    (SqlServerHostname, SQL_SERVER_HOSTNAME, b"sqlServerHostname", b"sql-server-hostname", b"suffixes.sqlServerHostname");
    /// The endpoint suffix for storage accounts.
    (Storage, STORAGE, b"storage", b"storage", b"suffixes.storage");
    /// The storage sync endpoint.
    (StorageSync, STORAGE_SYNC, b"storageSync", b"storage-sync", b"suffixes.storageSyncEndpointSuffix");
    /// The synapse analytics endpoint.
    (SynapseAnalytics, SYNAPSE_ANALYTICS, b"synapseAnalytics", b"synapse-analytics", b"suffixes.synapseAnalytics");
}

/// This map contains the built-in endpoint maps for the Azure clouds.
pub(super) static BUILTIN_ENDPOINT_MAPS: Lazy<HashMap<AzCloud, EndpointMap>> = Lazy::new(|| {
    HashMap::from([
        (
            AzCloud::AzureCloud,
            EndpointMap::from([
                (&ACTIVE_DIRECTORY_DATALAKE, "https://datalake.azure.net/"),
                (&ACR_LOGIN_SERVER, "azurecr.io"),
                (&ATTESTATION, "attest.azure.net"),
                (&AUTHENTICATION, "https://login.microsoftonline.com"),
                (
                    &AZURE_DATALAKE_ANALYTICS_CATALOG_AND_JOB,
                    "azuredatalakeanalytics.net",
                ),
                (&AZURE_DATALAKE_STORE_FILE_SYSTEM, "azuredatalakestore.net"),
                (&AZURE_FRONT_DOOR, "azurefd.net"),
                (&BATCH, "https://batch.core.windows.net/"),
                (&GALLERY, "https://gallery.azure.com/"),
                (&GRAPH, "https://graph.windows.net/"),
                (&KEY_VAULT_DNS, "vault.azure.net"),
                (&MARIADB_SERVER, "mariadb.database.azure.com"),
                (&MEDIA, "https://rest.media.azure.net"),
                (&MHSM_DNS, "managedhsm.azure.net"),
                (&MYSQL_SERVER, "mysql.database.azure.com"),
                (&PORTAL, "https://portal.azure.com"),
                (&POSTGRESQL_SERVER, "postgres.database.azure.com"),
                (&RESOURCE_MANAGER, "https://management.azure.com/"),
                (&SQL_MANAGEMENT, "https://management.core.windows.net:8443/"),
                (&SQL_SERVER_HOSTNAME, "database.windows.net"),
                (&STORAGE, "core.windows.net"),
                (&STORAGE_SYNC, "afs.azure.net"),
                (&SYNAPSE_ANALYTICS, "dev.azuresynapse.net"),
                (&VM_IMAGE_ALIAS_DOC, "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/arm-compute/quickstart-templates/aliases.json"),
            ]),
        ),
        (
            AzCloud::AzureChinaCloud,
            EndpointMap::from([
                (&ACR_LOGIN_SERVER, "azurecr.cn"),
                (&AUTHENTICATION, "https://login.chinacloudapi.cn"),
                (&BATCH, "https://batch.chinacloudapi.cn/"),
                (&GALLERY, "https://gallery.chinacloudapi.cn/"),
                (&GRAPH, "https://graph.chinacloudapi.cn/"),
                (&KEY_VAULT_DNS, "vault.azure.cn"),
                (&MARIADB_SERVER, "mariadb.database.chinacloudapi.cn"),
                (&MEDIA, "https://rest.media.chinacloudapi.cn"),
                (&MHSM_DNS, "managedhsm.azure.cn"),
                (&MYSQL_SERVER, "mysql.database.chinacloudapi.cn"),
                (&PORTAL, "https://portal.azure.cn"),
                (&POSTGRESQL_SERVER, "postgres.database.chinacloudapi.cn"),
                (&RESOURCE_MANAGER, "https://management.chinacloudapi.cn"),
                (
                    &SQL_MANAGEMENT,
                    "https://management.core.chinacloudapi.cn:8443/",
                ),
                (&SQL_SERVER_HOSTNAME, "database.chinacloudapi.cn"),
                (&STORAGE, "core.chinacloudapi.cn"),

                (&SYNAPSE_ANALYTICS, "dev.azuresynapse.azure.cn"),
                (&VM_IMAGE_ALIAS_DOC, "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/arm-compute/quickstart-templates/aliases.json"),

            ]),
        ),
        (
            AzCloud::AzureUSGovernment,
            EndpointMap::from([
                (&ACR_LOGIN_SERVER, "azurecr.us"),
                (&AUTHENTICATION, "https://login.microsoftonline.us"),
                (&BATCH, "https://batch.core.usgovcloudapi.net/"),
                (&GALLERY, "https://gallery.usgovcloudapi.net/"),
                (&GRAPH, "https://graph.windows.net/"),
                (&KEY_VAULT_DNS, "vault.usgovcloudapi.net"),
                (&MARIADB_SERVER, "mariadb.database.usgovcloudapi.net"),
                (&MEDIA, "https://rest.media.usgovcloudapi.net"),
                (&MHSM_DNS, "managedhsm.usgovcloudapi.net"),
                (&MYSQL_SERVER, "mysql.database.usgovcloudapi.net"),
                (&PORTAL, "https://portal.azure.us"),
                (&POSTGRESQL_SERVER, "postgres.database.usgovcloudapi.net"),
                (&RESOURCE_MANAGER, "https://management.usgovcloudapi.net/"),
                (
                    &SQL_MANAGEMENT,
                    "https://management.core.usgovcloudapi.net:8443/",
                ),
                (&SQL_SERVER_HOSTNAME, "database.usgovcloudapi.net"),
                (&STORAGE, "core.usgovcloudapi.net"),
                (&STORAGE_SYNC, "afs.azure.us"),

                (
                    &SYNAPSE_ANALYTICS,
                    "dev.azuresynapse.usgovcloudapi.net",
                ),
                (&VM_IMAGE_ALIAS_DOC, "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/arm-compute/quickstart-templates/aliases.json"),

            ]),
        ),
        (
            AzCloud::AzureGermanCloud,
            EndpointMap::from([
                (&AUTHENTICATION, "https://login.microsoftonline.de"),
                (&BATCH, "https://batch.cloudapi.de/"),
                (&GALLERY, "https://gallery.cloudapi.de/"),
                (&GRAPH, "https://graph.cloudapi.de/"),
                (&KEY_VAULT_DNS, "vault.microsoftazure.de"),
                (&MARIADB_SERVER, "mariadb.database.cloudapi.de"),
                (&MEDIA, "https://rest.media.cloudapi.de"),
                (&MHSM_DNS, "managedhsm.microsoftazure.de"),
                (&MYSQL_SERVER, "mysql.database.cloudapi.de"),
                (&PORTAL, "https://portal.microsoftazure.de"),
                (&POSTGRESQL_SERVER, "postgres.database.cloudapi.de"),
                (&RESOURCE_MANAGER, "https://management.microsoftazure.de"),
                (&SQL_MANAGEMENT, "https://management.core.cloudapi.de:8443/"),
                (&SQL_SERVER_HOSTNAME, "database.cloudapi.de"),
                (&STORAGE, "core.cloudapi.de"),
                (&VM_IMAGE_ALIAS_DOC, "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/arm-compute/quickstart-templates/aliases.json"),
            ]),
        ),
    ])
});
