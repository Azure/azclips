pub mod endpoint;
pub mod resource_id;

use endpoint::{EndpointName, BUILTIN_ENDPOINT_MAPS};
use resource_id::{ResourceIdName, BUILTIN_RESOURCE_ID_MAPS};

use super::AzCloud;
use serde::{ser::SerializeMap, Deserialize, Serialize, Serializer};
use tracing::error;

#[derive(Debug, Default, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AzCloudMetadata {
    // When serialize into cfg file the name will be ignored, so use the default value of AzCloud.
    // The actual value will be set during AzCloudCfg during deserialization.
    #[serde(default)]
    pub name: AzCloud,
    #[serde(default)]
    pub(crate) active: bool,
    #[serde(default)]
    pub(crate) endpoints: endpoint::EndpointMap,
    #[serde(default)]
    pub(crate) resource_ids: resource_id::ResourceIdMap,
}

impl AzCloudMetadata {
    pub async fn get_endpoint(&self, name: &EndpointName) -> Option<String> {
        let endpoint = self.get_endpoint_from_custom_or_builtin(name);
        if endpoint.is_some() {
            return endpoint;
        }
        if let Some(var_str) = name.get_var_str() {
            return self.get_value_from_metadata_api(var_str).await;
        }
        None
    }

    pub async fn get_resource_id(&self, name: &ResourceIdName) -> Option<String> {
        let resource_id = self.get_resource_id_from_custom_or_builtin(name);
        if resource_id.is_some() {
            return resource_id;
        }
        if let Some(var_str) = name.get_var_str() {
            return self.get_value_from_metadata_api(var_str).await;
        }
        None
    }

    pub async fn get_value_from_metadata_api(&self, _: &str) -> Option<String> {
        if let Some(mut endpoint) =
            self.get_endpoint_from_custom_or_builtin(&endpoint::RESOURCE_MANAGER)
        {
            if endpoint.ends_with("/") {
                endpoint.pop();
            }
            let _ = format!("{}/metadata/endpoints?api-version=2025-01-01", endpoint);
            // TODO: make request to the metadata api and parse the response
            // using the var_str to get the value
        } else {
            error!("No resource manager endpoint found cloud: {}", self.name);
        }
        None
    }

    fn get_endpoint_from_custom_or_builtin(&self, name: &EndpointName) -> Option<String> {
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
        None
    }

    fn get_resource_id_from_custom_or_builtin(&self, name: &ResourceIdName) -> Option<String> {
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
        None
    }
}

impl Serialize for AzCloudMetadata {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut map = serializer.serialize_map(None)?;

        // Always serialize name
        map.serialize_entry("name", &self.name)?;

        // Only serialize active if it's true
        if self.active {
            map.serialize_entry("active", &self.active)?;
        }

        // Only serialize endpoints if it's not empty
        let endpoints = self.endpoints.0.read().unwrap();
        if !endpoints.is_empty() {
            map.serialize_entry("endpoints", &self.endpoints)?;
        }

        // Only serialize resourceIds if it's not empty
        let resource_ids = self.resource_ids.0.read().unwrap();
        if !resource_ids.is_empty() {
            map.serialize_entry("resourceIds", &self.resource_ids)?;
        }

        map.end()
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
            active: true,
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
        assert!(json["name"] == "AzureCloud");
        assert!(json["active"] == true);
        assert!(json["endpoints"]["authentication"] == "https://test.endpoint.com");
        assert!(json["resourceIds"]["authentication"] == "https://test.resource.com/");
    }

    #[test]
    fn test_serialize_empty_metadata() {
        // the empty endpoints and resource_ids should not be ignored
        let metadata = AzCloudMetadata {
            name: AzCloud::AzureCloud,
            active: false,
            endpoints: Default::default(),
            resource_ids: Default::default(),
        };

        let json = serde_json::to_value(&metadata).unwrap();
        assert!(json["name"] == "AzureCloud");
        assert!(!json.as_object().unwrap().contains_key("active"));
        assert!(!json.as_object().unwrap().contains_key("endpoints"));
        assert!(!json.as_object().unwrap().contains_key("resourceIds"));
    }

    #[test]
    fn test_deserialize_metadata_without_name() {
        let json = json!({
            "endpoints": {
                "authentication": "https://test.endpoint.com"
            },
            "resourceIds": {
                "authentication": "https://test.resource.com/"
            }
        });

        let metadata: AzCloudMetadata = serde_json::from_value(json).unwrap();
        assert_eq!(metadata.name, AzCloud::AzureCloud); // Default value, the name field will be set during AzCloudCfg deserialization based on the hashmap key
        assert!(!metadata.active);
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

    #[test]
    fn test_deserialize_metadata_without_resource_ids() {
        let json = json!({
            "name": "AzureChinaCloud",
            "active": true,
            "endpoints": {
                "authentication": "https://test.endpoint.com"
            },
        });

        let metadata: AzCloudMetadata = serde_json::from_value(json).unwrap();
        assert_eq!(metadata.name, AzCloud::AzureChinaCloud); // Default value, the name field will be set during AzCloudCfg deserialization based on the hashmap key
        assert!(metadata.active);
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
        assert!(metadata.resource_ids.0.read().unwrap().is_empty());
    }

    #[test]
    fn test_deserialize_metadata_without_endpoints() {
        let json = json!({
            "name": "AzureChinaCloud",
            "active": false,
            "resourceIds": {
                "authentication": "https://test.resource.com/"
            }
        });

        let metadata: AzCloudMetadata = serde_json::from_value(json).unwrap();
        assert_eq!(metadata.name, AzCloud::AzureChinaCloud); // Default value, the name field will be set during AzCloudCfg deserialization based on the hashmap key
        assert!(!metadata.active);
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
        assert!(metadata.endpoints.0.read().unwrap().is_empty());
    }
}
