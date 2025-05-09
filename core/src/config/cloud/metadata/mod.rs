pub mod endpoint;
pub mod resource_id;

use endpoint::{EndpointName, BUILTIN_ENDPOINT_MAPS};
use resource_id::{ResourceIdName, BUILTIN_RESOURCE_ID_MAPS};

use super::AzCloud;

use serde::de::{MapAccess, Visitor};
use serde::ser::SerializeMap;
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::fmt;

#[derive(Debug, Clone)]
pub struct AzCloudMetadata {
    pub name: AzCloud,
    pub(crate) endpoints: endpoint::EndpointMap,
    pub(crate) resource_ids: resource_id::ResourceIdMap,
}

impl AzCloudMetadata {
    pub async fn get_endpoint(&self, name: &EndpointName) -> Option<String> {
        if let Some(endpoint) = self.endpoints.get(name) {
            // from custom configuration
            return Some(endpoint);
        }
        if let Some(build_in_map) = BUILTIN_ENDPOINT_MAPS.get(&self.name) {
            if let Some(endpoint) = build_in_map.get(name) {
                // from built-in configuration
                return Some(endpoint);
            }
        }
        // TODO: get endpoint from metadata endpoint
        None
    }

    pub async fn get_resource_id(&self, name: &ResourceIdName) -> Option<String> {
        if let Some(resource_id) = self.resource_ids.get(name) {
            // from custom configuration
            return Some(resource_id);
        }
        if let Some(build_in_map) = BUILTIN_RESOURCE_ID_MAPS.get(&self.name) {
            if let Some(resource_id) = build_in_map.get(name) {
                // from built-in configuration
                return Some(resource_id);
            }
        }
        // TODO: get resource id from metadata endpoint
        None
    }
}

// Custom serialization implementation for AzCloudMetadata
impl Serialize for AzCloudMetadata {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut map = serializer.serialize_map(Some(2))?;

        {
            // Only serialize non-empty endpoints
            let endpoints = self.endpoints.0.read().unwrap();
            if !endpoints.is_empty() {
                map.serialize_entry("endpoints", &self.endpoints)?;
            }
        }

        {
            // Only serialize non-empty resource_ids
            let resource_ids = self.resource_ids.0.read().unwrap();
            if !resource_ids.is_empty() {
                map.serialize_entry("resource_ids", &self.resource_ids)?;
            }
        }

        map.end()
    }
}

impl<'de> Deserialize<'de> for AzCloudMetadata {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        #[derive(Deserialize)]
        #[serde(field_identifier, rename_all = "snake_case")]
        enum Field {
            Name,
            Endpoints,
            ResourceIds,
        }

        struct AzCloudMetadataVisitor;

        impl<'de> Visitor<'de> for AzCloudMetadataVisitor {
            type Value = AzCloudMetadata;

            fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
                formatter.write_str("struct AzCloudMetadata")
            }

            fn visit_map<V>(self, mut map: V) -> Result<AzCloudMetadata, V::Error>
            where
                V: MapAccess<'de>,
            {
                let mut endpoints = None;
                let mut resource_ids = None;

                while let Some(key) = map.next_key()? {
                    match key {
                        Field::Name => {
                            // Ignore name field
                            let _: serde::de::IgnoredAny = map.next_value()?;
                        }
                        Field::Endpoints => {
                            if endpoints.is_some() {
                                return Err(serde::de::Error::duplicate_field("endpoints"));
                            }
                            endpoints = Some(map.next_value()?);
                        }
                        Field::ResourceIds => {
                            if resource_ids.is_some() {
                                return Err(serde::de::Error::duplicate_field("resource_ids"));
                            }
                            resource_ids = Some(map.next_value()?);
                        }
                    }
                }

                Ok(AzCloudMetadata {
                    name: AzCloud::AzureCloud, // Default value, actual value will be set during AzCloudCfg deserialization
                    endpoints: endpoints.unwrap_or_default(),
                    resource_ids: resource_ids.unwrap_or_default(),
                })
            }
        }

        deserializer.deserialize_struct(
            "AzCloudMetadata",
            &["name", "endpoints", "resource_ids"],
            AzCloudMetadataVisitor,
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::cloud::metadata::{endpoint, resource_id};
    use serde_json::json;

    #[test]
    fn test_serialize_metadata_without_name() {
        // the name field should be ignored in the serialization as the hashmap key is the cloud name
        let metadata = AzCloudMetadata {
            name: AzCloud::AzureCloud,
            endpoints: endpoint::EndpointMap::from([(
                &endpoint::AUTHENTICATION,
                "https://test.endpoint.com",
            )]),
            resource_ids: resource_id::ResourceIdMap::from([(
                &resource_id::AUTHENTICATION,
                "https://test.resource.com/",
            )]),
        };

        let json = serde_json::to_value(&metadata).unwrap();
        assert!(!json.as_object().unwrap().contains_key("name"));
        assert!(json["endpoints"]["authentication"] == "https://test.endpoint.com");
        assert!(json["resource_ids"]["authentication"] == "https://test.resource.com/");
    }

    #[test]
    fn test_serialize_empty_metadata() {
        // the empty endpoints and resource_ids should not be ignored
        let metadata = AzCloudMetadata {
            name: AzCloud::AzureCloud,
            endpoints: Default::default(),
            resource_ids: Default::default(),
        };

        let json = serde_json::to_value(&metadata).unwrap();
        assert!(!json.as_object().unwrap().contains_key("name"));
        assert!(!json.as_object().unwrap().contains_key("endpoints"));
        assert!(!json.as_object().unwrap().contains_key("resource_ids"));
    }

    #[test]
    fn test_deserialize_metadata_without_name() {
        let json = json!({
            "endpoints": {
                "authentication": "https://test.endpoint.com"
            },
            "resource_ids": {
                "authentication": "https://test.resource.com/"
            }
        });

        let metadata: AzCloudMetadata = serde_json::from_value(json).unwrap();
        assert_eq!(metadata.name, AzCloud::AzureCloud); // Default value, the name field will be set during AzCloudCfg deserialization based on the hashmap key
        assert_eq!(
            metadata
                .endpoints
                .0
                .read()
                .unwrap()
                .get(&endpoint::AUTHENTICATION)
                .unwrap(),
            "https://test.endpoint.com"
        );
        assert_eq!(
            metadata
                .resource_ids
                .0
                .read()
                .unwrap()
                .get(&resource_id::AUTHENTICATION)
                .unwrap(),
            "https://test.resource.com/"
        );
    }
}
