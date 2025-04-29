use std::str::FromStr;

use serde::{Deserialize, Serialize};

use crate::cloud::AzCloud;

#[derive(Debug, Default, Serialize, Deserialize)]
pub(crate) struct AzCloudCfg {
    #[serde(
        serialize_with = "serialize_az_cloud",
        deserialize_with = "deserialize_az_cloud"
    )]
    pub(crate) name: AzCloud,
}

fn serialize_az_cloud<S>(cloud: &AzCloud, serializer: S) -> Result<S::Ok, S::Error>
where
    S: serde::Serializer,
{
    serializer.serialize_str(&cloud.to_string())
}

fn deserialize_az_cloud<'de, D>(deserializer: D) -> Result<AzCloud, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let s = String::deserialize(deserializer)?;
    Ok(AzCloud::from_str(&s).unwrap())
}
