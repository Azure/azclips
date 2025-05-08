pub mod audience;
pub mod endpoint;

use audience::{AudienceName, BUILTIN_AUDIENCE_MAPS};
use endpoint::{EndpointName, BUILTIN_ENDPOINT_MAPS};
use serde::{Deserialize, Serialize};

use super::AzCloud;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzCloudMetadata {
    pub(crate) name: AzCloud,
    pub(crate) endpoints: endpoint::EndpointMap,
    pub(crate) audiences: audience::AudienceMap,
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

impl AzCloudMetadata {
    pub fn get_endpoint(&self, endpoint: &EndpointName) -> Option<&str> {
        if let Some(endpoint) = self.endpoints.get(endpoint) {
            // from custom configuration
            Some(endpoint)
        } else if let Some(endpoint) = BUILTIN_ENDPOINT_MAPS
            .get(&self.name)
            .and_then(|build_in_map| build_in_map.get(endpoint))
        {
            // from built-in configuration
            Some(endpoint)
        } else {
            // TODO: get endpoint from metadata endpoint
            None
        }
    }

    pub fn get_audience(&self, audience: &AudienceName) -> Option<&str> {
        if let Some(audience) = self.audiences.get(audience) {
            // from custom configuration
            Some(audience)
        } else if let Some(audience) = BUILTIN_AUDIENCE_MAPS
            .get(&self.name)
            .and_then(|build_in_map| build_in_map.get(audience))
        {
            // from built-in configuration
            Some(audience)
        } else {
            // TODO: get audience from metadata endpoint
            None
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_get_endpoint() {
        let mut metadata = AzCloudMetadata::default();
        assert_eq!(metadata.name, AzCloud::AzureCloud);
        let endpoint = metadata.get_endpoint(&endpoint::AUTHENTICATION);
        assert_eq!(endpoint, Some("https://login.microsoftonline.com"));
        let audience = metadata.get_audience(&audience::AUTHENTICATION);
        assert_eq!(audience, Some("https://management.core.windows.net/"));

        metadata
            .endpoints
            .insert(endpoint::AUTHENTICATION, "https://custom.endpoint.com");
        metadata
            .audiences
            .insert(audience::AUTHENTICATION, "https://custom.endpoint.com/");

        let endpoint = metadata.get_endpoint(&endpoint::AUTHENTICATION);
        assert_eq!(endpoint, Some("https://custom.endpoint.com"));
        let audience = metadata.get_audience(&audience::AUTHENTICATION);
        assert_eq!(audience, Some("https://custom.endpoint.com/"));
    }
}
