use std::collections::HashMap;
use std::fmt;

use serde::de::{self, MapAccess, Visitor};
use serde::ser::SerializeMap;
use serde::{Deserialize, Deserializer, Serialize, Serializer};

use crate::error::AzUserUsageError;

use super::{metadata::AzCloudMetadata, AzCloud};

#[derive(Debug, Clone)]
pub struct AzCloudCfg {
    /// The current active cloud name
    pub(crate) active: AzCloud,

    /// Customized cloud configurations
    pub(crate) clouds: HashMap<AzCloud, AzCloudMetadata>,
}

impl Default for AzCloudCfg {
    fn default() -> Self {
        let mut clouds = HashMap::new();
        clouds.insert(
            AzCloud::AzureCloud,
            AzCloudMetadata {
                name: AzCloud::AzureCloud,
                endpoints: Default::default(),
                resource_ids: Default::default(),
            },
        );
        Self {
            active: AzCloud::AzureCloud,
            clouds,
        }
    }
}

impl AzCloudCfg {
    pub fn forbid_telemetry(&self) -> bool {
        match &self.active {
            // Add names of clouds that don't allow telemetry data collection here such as some air-gapped clouds.
            AzCloud::Custom(custom_name) => {
                custom_name.eq_ignore_ascii_case("USSec")
                    || custom_name.eq_ignore_ascii_case("USNat")
            }
            _ => false,
        }
    }

    pub(crate) fn get_cloud(&self, name: &AzCloud) -> Result<AzCloudMetadata, AzUserUsageError> {
        if matches!(name, AzCloud::Custom(_)) && !self.clouds.contains_key(&name) {
            return Err(AzUserUsageError::CloudUnregisteredError(name.to_string()));
        }
        let metadata = self.clouds.get(name).map_or(
            AzCloudMetadata {
                name: name.clone(),
                endpoints: Default::default(),
                resource_ids: Default::default(),
            },
            |metadata| metadata.clone(),
        );
        Ok(metadata)
    }

    /// This function should only be called by the AzCore, don't call it in other places
    /// If you want to switch the cloud, please use the `switch_cloud` function in the AzCore
    pub(crate) fn set_active(
        &mut self,
        active: AzCloud,
    ) -> Result<&AzCloudMetadata, AzUserUsageError> {
        if matches!(active, AzCloud::Custom(_)) && !self.clouds.contains_key(&active) {
            return Err(AzUserUsageError::CloudUnregisteredError(active.to_string()));
        }

        self.active = active.clone();
        let metadata = self
            .clouds
            .entry(active.clone())
            .or_insert(AzCloudMetadata {
                name: active.clone(),
                endpoints: Default::default(),
                resource_ids: Default::default(),
            });
        Ok(metadata)
    }

    pub fn get_active(&self) -> &AzCloudMetadata {
        self.clouds
            .get(&self.active)
            .expect("active cloud should be in the clouds map due to the deserializer")
    }
}

impl Serialize for AzCloudCfg {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut map = serializer.serialize_map(Some(2))?;
        map.serialize_entry("active", &self.active)?;

        // Only serialize clouds if it's not empty
        if !self.clouds.is_empty() {
            map.serialize_entry("clouds", &self.clouds)?;
        }

        map.end()
    }
}

impl<'de> Deserialize<'de> for AzCloudCfg {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        #[derive(Deserialize)]
        #[serde(field_identifier, rename_all = "lowercase")]
        enum Field {
            Active,
            Clouds,
        }

        struct AzCloudCfgVisitor;

        impl<'de> Visitor<'de> for AzCloudCfgVisitor {
            type Value = AzCloudCfg;

            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("struct AzCloudCfg")
            }

            fn visit_map<V>(self, mut map: V) -> Result<AzCloudCfg, V::Error>
            where
                V: MapAccess<'de>,
            {
                let mut active: Option<AzCloud> = None;
                let mut clouds: Option<HashMap<AzCloud, AzCloudMetadata>> = None;

                while let Some(key) = map.next_key()? {
                    match key {
                        Field::Active => {
                            if active.is_some() {
                                return Err(de::Error::duplicate_field("active"));
                            }
                            active = Some(map.next_value()?);
                        }
                        Field::Clouds => {
                            if clouds.is_some() {
                                return Err(de::Error::duplicate_field("clouds"));
                            }
                            clouds = Some(map.next_value()?);
                        }
                    }
                }

                let active = active.unwrap_or(AzCloud::AzureCloud);
                let mut clouds = clouds.unwrap_or_default();

                // Ensure the active cloud is in the clouds map
                if !clouds.contains_key(&active) {
                    clouds.insert(
                        active.clone(),
                        AzCloudMetadata {
                            name: active.clone(),
                            endpoints: Default::default(),
                            resource_ids: Default::default(),
                        },
                    );
                }

                // Fill in the name field for each cloud metadata
                for (cloud_name, metadata) in clouds.iter_mut() {
                    metadata.name = cloud_name.clone();
                }

                Ok(AzCloudCfg { active, clouds })
            }
        }

        deserializer.deserialize_struct("AzCloudCfg", &["active", "clouds"], AzCloudCfgVisitor)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::cloud::metadata::{endpoint, resource_id};
    use serde_json::json;

    #[tokio::test]
    async fn test_get_active() {
        let mut cfg = AzCloudCfg::default();
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
        let cfg: AzCloudCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.active, AzCloud::AzureCloud);
        assert!(cfg.clouds.contains_key(&AzCloud::AzureCloud));
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
            "active": "AzureChinaCloud"
        });
        let cfg: AzCloudCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.active, AzCloud::AzureChinaCloud);
        assert!(cfg.clouds.contains_key(&AzCloud::AzureChinaCloud));
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
            "active": "AzureCloud",
            "clouds": {
                "AzureCloud": {
                    "endpoints": {
                        "authentication": "https://test.resource.com"
                    },
                    "resourceIds": {
                        "authentication": "https://test.resource.com/"
                    }
                }
            }
        });
        let cfg: AzCloudCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.active, AzCloud::AzureCloud);
        assert!(cfg.clouds.contains_key(&AzCloud::AzureCloud));
        let active = cfg.get_active();
        let endpoint = active.get_endpoint(&endpoint::AUTHENTICATION).await;
        assert_eq!(endpoint.as_ref().unwrap(), "https://test.resource.com");
        let resource_id = active.get_resource_id(&resource_id::AUTHENTICATION).await;
        assert_eq!(resource_id.as_ref().unwrap(), "https://test.resource.com/");
    }

    #[tokio::test]
    async fn test_deserialize_with_custom_cloud() {
        let json = json!({
            "active": "CustomCloud",
            "clouds": {
                "AzureCloud": {
                    "name": "AzureCloud",
                    "endpoints": {
                        "authentication": "https://test.resource.com"
                    },
                },
                "CustomCloud": {
                    "name": "CustomCloud",
                    "endpoints": {
                        "authentication": "https://test.custom.resource.com"
                    },
                    "resourceIds": {
                        "authentication": "https://test.custom.resource.com/"
                    }
                }
            }
        });
        let mut cfg: AzCloudCfg = serde_json::from_value(json).unwrap();
        assert_eq!(cfg.active, AzCloud::Custom("CustomCloud".to_string()));
        assert!(cfg
            .clouds
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
        let mut cfg = AzCloudCfg {
            active: AzCloud::AzureCloud,
            clouds: HashMap::new(),
        };
        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(json["active"], "AzureCloud");
        assert!(!json.as_object().unwrap().contains_key("clouds"));

        cfg.set_active(AzCloud::AzureCloud).unwrap();
        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "active": "AzureCloud",
                "clouds": {
                    "AzureCloud": {
                        "name": "AzureCloud",
                        "endpoints": {},
                        "resourceIds": {},
                    }
                }
            })
        );

        cfg.set_active(AzCloud::AzureChinaCloud).unwrap();
        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "active": "AzureChinaCloud",
                "clouds": {
                    "AzureCloud": {
                        "name": "AzureCloud",
                        "endpoints": {},
                        "resourceIds": {},
                    },
                    "AzureChinaCloud": {
                        "name": "AzureChinaCloud",
                        "endpoints": {},
                        "resourceIds": {},
                    }
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

        let cfg = AzCloudCfg {
            active: AzCloud::AzureCloud,
            clouds,
        };

        let json = serde_json::to_value(&cfg).unwrap();
        assert_eq!(
            json,
            json!({
                "active": "AzureCloud",
                "clouds": {
                    "AzureCloud": {
                        "name": "AzureCloud",
                        "endpoints": {
                            "authentication": "https://test.endpoint.com"
                        },
                        "resourceIds": {
                            "authentication": "https://test.resource.com/"
                        }
                    }
                }
            })
        )
    }
}
