use std::collections::HashMap;
use std::fmt;

use serde::de::{MapAccess, Visitor};
use serde::{Deserialize, Deserializer, Serialize};
use tracing::warn;

use crate::error::AzUserUsageError;

use super::{metadata::AzCloudMetadata, AzCloud};

#[derive(Debug, Clone, Serialize)]
pub struct AzCloudsCfg(pub(crate) HashMap<AzCloud, AzCloudMetadata>);

impl Default for AzCloudsCfg {
    fn default() -> Self {
        let mut clouds = HashMap::new();
        clouds.insert(
            AzCloud::AzureCloud,
            AzCloudMetadata {
                name: AzCloud::AzureCloud,
                active: true,
                endpoints: Default::default(),
                resource_ids: Default::default(),
            },
        );
        Self(clouds)
    }
}

impl AzCloudsCfg {
    // Will be used in the future
    // pub fn forbid_telemetry(&self) -> bool {
    //     match &self.get_active().name {
    //         // Add names of clouds that don't allow telemetry data collection here such as some air-gapped clouds.
    //         AzCloud::Custom(custom_name) => {
    //             custom_name.eq_ignore_ascii_case("USSec")
    //                 || custom_name.eq_ignore_ascii_case("USNat")
    //         }
    //         _ => false,
    //     }
    // }

    pub fn get_cloud(&self, name: &AzCloud) -> Result<AzCloudMetadata, AzUserUsageError> {
        if matches!(name, AzCloud::Custom(_)) && !self.0.contains_key(name) {
            return Err(AzUserUsageError::CloudUnregisteredError(name.to_string()));
        }
        let metadata = self.0.get(name).map_or(
            AzCloudMetadata {
                name: name.clone(),
                active: false,
                endpoints: Default::default(),
                resource_ids: Default::default(),
            },
            |metadata| metadata.clone(),
        );
        Ok(metadata)
    }

    pub fn get_active(&self) -> &AzCloudMetadata {
        self.0
            .iter()
            .find(|(_, metadata)| metadata.active)
            .map(|(_, metadata)| metadata)
            .expect("active cloud should be in the clouds map due to the deserializer")
    }

    /// This function should only be called by the AzCore, don't call it in other places
    /// If you want to switch the cloud, please use the `switch_cloud` function in the AzCore
    pub fn set_active(&mut self, active: AzCloud) -> Result<&AzCloudMetadata, AzUserUsageError> {
        if matches!(active, AzCloud::Custom(_)) && !self.0.contains_key(&active) {
            return Err(AzUserUsageError::CloudUnregisteredError(active.to_string()));
        }

        self.0.iter_mut().for_each(|(_, metadata)| {
            metadata.active = false;
        });

        let metadata = self.0.entry(active.clone()).or_insert(AzCloudMetadata {
            name: active.clone(),
            active: false,
            endpoints: Default::default(),
            resource_ids: Default::default(),
        });

        metadata.active = true;

        Ok(metadata)
    }
}

impl<'de> Deserialize<'de> for AzCloudsCfg {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        struct AzCloudsCfgVisitor;

        impl<'de> Visitor<'de> for AzCloudsCfgVisitor {
            type Value = AzCloudsCfg;

            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("a map of cloud configurations")
            }

            fn visit_map<M>(self, mut map: M) -> Result<Self::Value, M::Error>
            where
                M: MapAccess<'de>,
            {
                let mut clouds = HashMap::new();
                let mut found_active = false;

                while let Some((cloud, mut metadata)) =
                    map.next_entry::<AzCloud, AzCloudMetadata>()?
                {
                    // If we already found an active cloud, set this one to inactive
                    if metadata.active && found_active {
                        warn!(
                            "Warning: Multiple active clouds found. Setting {} to inactive.",
                            cloud
                        );
                        metadata.active = false;
                    }
                    if metadata.active {
                        found_active = true;
                    }
                    clouds.insert(cloud, metadata);
                }

                // If no active cloud was found, set AzureCloud as active
                if !found_active {
                    warn!("Warning: No active cloud found. Setting AzureCloud as active.");
                    if let Some(metadata) = clouds.get_mut(&AzCloud::AzureCloud) {
                        metadata.active = true;
                    } else {
                        // If AzureCloud doesn't exist, create it
                        clouds.insert(
                            AzCloud::AzureCloud,
                            AzCloudMetadata {
                                name: AzCloud::AzureCloud,
                                active: true,
                                endpoints: Default::default(),
                                resource_ids: Default::default(),
                            },
                        );
                    }
                }

                Ok(AzCloudsCfg(clouds))
            }
        }

        deserializer.deserialize_map(AzCloudsCfgVisitor)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::cloud::metadata::{endpoint, resource_id};
    use serde_json::json;

    #[tokio::test]
    async fn test_get_active() {
        let mut cfg = AzCloudsCfg::default();
        {
            let active = cfg.get_active();
            assert_eq!(active.name, AzCloud::AzureCloud);
            let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
            assert_eq!(
                endpoint.as_ref().unwrap(),
                "https://login.microsoftonline.com"
            );
            let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
            assert_eq!(
                resource_id.as_ref().unwrap(),
                "https://management.core.windows.net/"
            );
        }
        {
            let active = cfg.set_active(AzCloud::AzureChinaCloud).unwrap();
            assert_eq!(active.name, AzCloud::AzureChinaCloud);
            let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
            assert_eq!(endpoint.as_ref().unwrap(), "https://login.chinacloudapi.cn");
            let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
            assert_eq!(
                resource_id.as_ref().unwrap(),
                "https://management.core.chinacloudapi.cn/"
            );
        }
        {
            let result = cfg.set_active(AzCloud::Custom("NewCloud".to_string()));
            assert!(matches!(
                result,
                Err(AzUserUsageError::CloudUnregisteredError(_))
            ))
        }
    }

    #[tokio::test]
    async fn test_deserialize_with_missing_every_field() {
        let json = json!({});
        let cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.get_active().name, AzCloud::AzureCloud);
        assert!(cfg.0.contains_key(&AzCloud::AzureCloud));
        let active = cfg.get_active();
        let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
        assert_eq!(
            endpoint.as_ref().unwrap(),
            "https://login.microsoftonline.com"
        );
        let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
        assert_eq!(
            resource_id.as_ref().unwrap(),
            "https://management.core.windows.net/"
        );
    }

    #[tokio::test]
    async fn test_deserialize_with_missing_clouds() {
        let json = json!({
            "AzureChinaCloud": {
                "name": "AzureChinaCloud",
                "active": true,
            }
        });
        let cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.get_active().name, AzCloud::AzureChinaCloud);
        assert!(cfg.0.contains_key(&AzCloud::AzureChinaCloud));
        let active = cfg.get_active();
        let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
        assert_eq!(endpoint.as_ref().unwrap(), "https://login.chinacloudapi.cn");
        let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
        assert_eq!(
            resource_id.as_ref().unwrap(),
            "https://management.core.chinacloudapi.cn/"
        );
    }

    #[tokio::test]
    async fn test_deserialize_with_all_fields() {
        let json = json!({
            "AzureCloud": {
                "name": "AzureCloud",
                "active": true,
                "endpoints": {
                    "authentication": "https://test.resource.com"
                },
                "resourceIds": {
                    "authentication": "https://test.resource.com/"
                }
            }
        });
        let cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.get_active().name, AzCloud::AzureCloud);
        assert!(cfg.0.contains_key(&AzCloud::AzureCloud));
        let active = cfg.get_active();
        let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
        assert_eq!(endpoint.as_ref().unwrap(), "https://test.resource.com");
        let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
        assert_eq!(resource_id.as_ref().unwrap(), "https://test.resource.com/");
    }

    #[tokio::test]
    async fn test_deserialize_with_custom_cloud() {
        let json = json!({
            "AzureCloud": {
                "name": "AzureCloud",
                "endpoints": {
                    "authentication": "https://test.resource.com"
                },
            },
            "CustomCloud": {
                "name": "CustomCloud",
                "active": true,
                "endpoints": {
                    "authentication": "https://test.custom.resource.com"
                },
                "resourceIds": {
                    "authentication": "https://test.custom.resource.com/"
                }
            }
        });
        let mut cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(
            cfg.get_active().name,
            AzCloud::Custom("CustomCloud".to_string())
        );
        assert!(cfg
            .0
            .contains_key(&AzCloud::Custom("CustomCloud".to_string())));

        {
            let active = cfg.get_active();
            let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
            assert_eq!(
                endpoint.as_ref().unwrap(),
                "https://test.custom.resource.com"
            );
            let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
            assert_eq!(
                resource_id.as_ref().unwrap(),
                "https://test.custom.resource.com/"
            );
        }

        {
            let active = cfg.set_active(AzCloud::AzureCloud).unwrap();
            assert_eq!(active.name, AzCloud::AzureCloud);
            let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
            assert_eq!(endpoint.as_ref().unwrap(), "https://test.resource.com");
            let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
            assert_eq!(
                resource_id.as_ref().unwrap(),
                "https://management.core.windows.net/"
            );
        }
    }

    #[test]
    fn test_serialize_empty_clouds() {
        let mut cfg = AzCloudsCfg::default();
        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "AzureCloud": {
                    "name": "AzureCloud",
                    "active": true,
                }
            })
        );
        cfg.set_active(AzCloud::AzureCloud).unwrap();
        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "AzureCloud": {
                    "name": "AzureCloud",
                    "active": true,
                }
            })
        );

        cfg.set_active(AzCloud::AzureChinaCloud).unwrap();
        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "AzureCloud": {
                    "name": "AzureCloud",
                },
                "AzureChinaCloud": {
                    "name": "AzureChinaCloud",
                    "active": true,
                }
            })
        );
    }

    #[test]
    fn test_serialize_with_clouds() {
        let mut clouds = HashMap::new();
        clouds.insert(
            AzCloud::AzureCloud,
            AzCloudMetadata {
                name: AzCloud::AzureCloud,
                active: true,
                endpoints: endpoint::EndpointMap::from([(
                    &endpoint::AUTHENTICATION,
                    "https://test.endpoint.com",
                )]),
                resource_ids: resource_id::ResourceIdMap::from([(
                    &resource_id::AUTHENTICATION,
                    "https://test.resource.com/",
                )]),
            },
        );

        let cfg = AzCloudsCfg(clouds);

        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "AzureCloud": {
                    "name": "AzureCloud",
                    "active": true,
                    "endpoints": {
                        "authentication": "https://test.endpoint.com"
                    },
                    "resourceIds": {
                        "authentication": "https://test.resource.com/"
                    }
                }
            })
        )
    }

    #[tokio::test]
    async fn test_deserialize_multiple_active_clouds() {
        let json = json!({
            "AzureCloud": {
                "name": "AzureCloud",
                "active": true,
                "endpoints": {
                    "authentication": "https://test.resource.com"
                }
            },
            "AzureChinaCloud": {
                "name": "AzureChinaCloud",
                "active": true,
                "endpoints": {
                    "authentication": "https://test.china.resource.com"
                }
            }
        });
        let cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.get_active().name, AzCloud::AzureChinaCloud);
        assert!(!cfg.0.get(&AzCloud::AzureCloud).unwrap().active);
    }

    #[tokio::test]
    async fn test_deserialize_no_active_cloud() {
        let json = json!({
            "AzureCloud": {
                "name": "AzureCloud",
                "active": false,
                "endpoints": {
                    "authentication": "https://test.resource.com"
                }
            },
            "AzureChinaCloud": {
                "name": "AzureChinaCloud",
                "active": false,
                "endpoints": {
                    "authentication": "https://test.china.resource.com"
                }
            }
        });
        let cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.get_active().name, AzCloud::AzureCloud);
        assert!(cfg.0.get(&AzCloud::AzureCloud).unwrap().active);

        // will insert AzureCloud as active cloud
        let json = json!({
            "AzureChinaCloud": {
                "name": "AzureChinaCloud",
                "active": false,
                "endpoints": {
                    "authentication": "https://test.china.resource.com"
                }
            }
        });
        let cfg: AzCloudsCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.get_active().name, AzCloud::AzureCloud);
        assert!(cfg.0.get(&AzCloud::AzureCloud).unwrap().active);
    }
}
