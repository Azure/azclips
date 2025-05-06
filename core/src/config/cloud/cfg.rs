use serde::{Deserialize, Serialize};

use super::AzCloud;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzCloudCfg {
    /// The current active cloud name
    #[serde(
        serialize_with = "AzCloud::serialize_az_cloud",
        deserialize_with = "AzCloud::deserialize_az_cloud"
    )]
    pub(crate) name: AzCloud,
}
