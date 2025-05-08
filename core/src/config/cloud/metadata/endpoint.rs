#[derive(Clone, Eq, PartialEq, Hash)]
pub struct EndpointName {
    inner: Repr,
}

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
/// * `$var_bytes` - The byte string literal for the index path to retrieve the value from [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
///
/// # Examples
///
/// ```
/// standard_endpoints! {
///     (ActiveDirectory, ACTIVE_DIRECTORY, b"activeDirectory", b"authentication.loginEndpoint");
/// }
/// ```
macro_rules! standard_endpoints {
        (
            $(
                $(#[$docs:meta])*
                ($konst:ident, $upcase:ident, $name_bytes:literal, $var_bytes:literal);
            )+
        ) => {
            #[derive(Debug, Clone, Copy, Eq, PartialEq, Hash)]
            enum StandardEndpoint {
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

/// This macro defines standard suffixes for Azure cloud services.
///
/// # Parameters
///
/// * `$konst` - The constant identifier for the suffix
/// * `$upcase` - The uppercase constant name
/// * `$name_bytes` - The byte string literal for the suffix name
/// * `$var_bytes` - The byte string literal for the index path to retrieve the value from [metadata api](https://management.azure.com/metadata/endpoints?api-version=2025-01-01)
///
/// # Examples
///
/// ```
/// standard_suffixes! {
///     (AcrLoginServer, ACR_LOGIN_SERVER, b"acrLoginServer", b"suffixes.acrLoginServer");
/// }
/// ```
macro_rules! standard_suffixes {
        (
            $(
                $(#[$docs:meta])*
                ($konst:ident, $upcase:ident, $name_bytes:literal, $var_bytes:literal);
            )+
        ) => {
            #[derive(Debug, Clone, Copy, Eq, PartialEq, Hash)]
            enum StandardSuffix {
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

standard_endpoints! {
    /// The Active Directory data lake endpoint.
    (ActiveDirectoryDataLake, ACTIVE_DIRECTORY_DATALAKE, b"activeDirectoryDataLake", b"activeDirectoryDataLake");
    /// The Authentication login endpoint
    (Authentication, AUTHENTICATION, b"authentication", b"authentication.loginEndpoint");
    /// The Azure Batch endpoint.
    (Batch, BATCH, b"batch", b"batch");
    /// The template gallery endpoint.
    (Gallery, GALLERY, b"gallery", b"gallery");
    /// The Active Directory resource identifier.
    (Graph, GRAPH, b"graph", b"graph");
    /// The media endpoint.
    (Media, MEDIA, b"media", b"media");
    /// The portal endpoint.
    (Portal, PORTAL, b"portal", b"portal");
    /// The resource management endpoint.
    (ResourceManager, RESOURCE_MANAGER, b"resourceManager", b"resourceManager");
    /// The sql server management endpoint.
    (SqlManagement, SQL_MANAGEMENT, b"sqlManagement", b"sqlManagement");
    /// The uri of the document which caches commonly used virtual machine images.
    (VmImageAliasDoc, VM_IMAGE_ALIAS_DOC, b"vmImageAliasDoc", b"vmImageAliasDoc");
}

standard_suffixes! {
    /// The Azure Container Registry login server suffix
    (AcrLoginServer, ACR_LOGIN_SERVER, b"acrLoginServer", b"suffixes.acrLoginServer");
    /// The attestation endpoint.
    (Attestation, ATTESTATION, b"attestation", b"suffixes.attestationEndpoint");
    /// The Data Lake analytics job and catalog service dns suffix.
    (AzureDatalakeAnalyticsCatalogAndJob, AZURE_DATALAKE_ANALYTICS_CATALOG_AND_JOB, b"azureDatalakeAnalyticsCatalogAndJob", b"suffixes.azureDataLakeAnalyticsCatalogAndJob");
    /// The Data Lake store filesystem service dns suffix.
    (AzureDatalakeStoreFileSystem, AZURE_DATALAKE_STORE_FILE_SYSTEM, b"azureDatalakeStoreFileSystem", b"suffixes.azureDataLakeStoreFileSystem");
    /// The Azure Front Door service dns suffix.
    (AzureFrontDoor, AZURE_FRONT_DOOR, b"azureFrontDoor", b"suffixes.azureFrontDoorEndpointSuffix");
    /// The Key Vault service dns suffix.
    (KeyVaultDns, KEY_VAULT_DNS, b"keyVaultDns", b"suffixes.keyVaultDns");
    /// The mariadb server endpoint.
    (MariadbServer, MARIADB_SERVER, b"mariadbServer", b"suffixes.mariadbServerEndpoint");
    /// The mhsm dns.
    (MhsmDns, MHSM_DNS, b"mhsmDns", b"suffixes.mhsmDns");
    /// The mysql server endpoint.
    (MysqlServer, MYSQL_SERVER, b"mysqlServer", b"suffixes.mysqlServerEndpoint");
    /// The postgres server endpoint.
    (PostgresqlServer, POSTGRESQL_SERVER, b"postgresqlServer", b"suffixes.postgresqlServerEndpoint");
    /// The dns suffix for sql servers.
    (SqlServerHostname, SQL_SERVER_HOSTNAME, b"sqlServerHostname", b"suffixes.sqlServerHostname");
    /// The endpoint suffix for storage accounts.
    (Storage, STORAGE, b"storage", b"suffixes.storage");
    /// The storage sync endpoint.
    (StorageSync, STORAGE_SYNC, b"storageSync", b"suffixes.storageSyncEndpointSuffix");
    /// The synapse analytics endpoint.
    (SynapseAnalytics, SYNAPSE_ANALYTICS, b"synapseAnalytics", b"suffixes.synapseAnalytics");
}
