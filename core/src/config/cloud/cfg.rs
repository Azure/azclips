use serde::{Deserialize, Serialize};

use super::AzCloud;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzCloudCfg {
    /// The current active cloud name
    pub(crate) name: AzCloud,
}
