mod audience;
mod endpoint;

use std::{
    collections::HashMap,
    fmt::{self, Display},
    str::FromStr,
};

use serde::{Deserialize, Serialize};

use super::AzCloud;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzCloudMetadata {
    #[serde(
        serialize_with = "AzCloud::serialize_az_cloud",
        deserialize_with = "AzCloud::deserialize_az_cloud"
    )]
    pub(crate) name: AzCloud,
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
