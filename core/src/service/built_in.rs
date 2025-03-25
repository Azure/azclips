use crate::cmd_uri::CmdUri;
use serde::{de::Error, ser::SerializeMap, Deserialize};
use std::str::FromStr;
use strum_macros::EnumIter;

#[derive(Debug, Clone, PartialEq, Eq, Hash, EnumIter)]
pub enum AzBuildInService {
    /// The login, logout, version, upgrade commands etc.
    Base,
    /// Manage azure subscription information.
    Account,
    /// Manage the registered Azure clouds.
    Cloud,
    /// Manage the configuration.
    Config,
    /// Manage the plugins.
    Plugin,
}

impl AzBuildInService {
    /// cmd_uri: `cmd://azure/`
    const BASE_URI: &'static str = "cmd://azure/";
    /// cmd_uri: `cmd://azure/account/`
    const ACCOUNT_URI: &'static str = "cmd://azure/account/";
    /// cmd_uri: `cmd://azure/cloud/`
    const CLOUD_URI: &'static str = "cmd://azure/cloud/";
    /// cmd_uri: `cmd://azure/config/`
    const CONFIG_URI: &'static str = "cmd://azure/config/";
    /// cmd_uri: `cmd://azure/plugin/`
    const PLUGIN_URI: &'static str = "cmd://azure/plugin/";

    // pub fn uri(&self) -> CmdUri {
    //     CmdUri::new(self.segments().to_vec(), false)
    // }

    pub fn uri(&self) -> CmdUri {
        match self {
            AzBuildInService::Base => CmdUri::from_str(Self::BASE_URI).unwrap(),
            AzBuildInService::Account => CmdUri::from_str(Self::ACCOUNT_URI).unwrap(),
            AzBuildInService::Cloud => CmdUri::from_str(Self::CLOUD_URI).unwrap(),
            AzBuildInService::Config => CmdUri::from_str(Self::CONFIG_URI).unwrap(),
            AzBuildInService::Plugin => CmdUri::from_str(Self::PLUGIN_URI).unwrap(),
        }
    }

    pub fn from_uri(uri: &CmdUri) -> Option<Self> {
        match uri.to_string().as_str() {
            s if s == Self::BASE_URI => Some(Self::Base),
            s if s == Self::ACCOUNT_URI => Some(Self::Account),
            s if s == Self::CLOUD_URI => Some(Self::Cloud),
            s if s == Self::CONFIG_URI => Some(Self::Config),
            s if s == Self::PLUGIN_URI => Some(Self::Plugin),
            _ => None,
        }
    }
}

pub(super) fn serialize_az_build_in_service<S>(
    service: &AzBuildInService,
    serializer: S,
) -> Result<S::Ok, S::Error>
where
    S: serde::Serializer,
{
    let mut map = serializer.serialize_map(Some(1))?;
    map.serialize_entry("uri", &service.uri().to_string())?;
    map.end()
}

pub(super) fn deserialize_az_build_in_service<'de, D>(
    deserializer: D,
) -> Result<AzBuildInService, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = serde_json::Value::deserialize(deserializer)?;
    let uri = value
        .get("uri")
        .and_then(|v| v.as_str())
        .ok_or_else(|| D::Error::custom("service uri is required"))?;
    let uri = CmdUri::from_str(uri).map_err(D::Error::custom)?;

    AzBuildInService::from_uri(&uri).ok_or_else(|| D::Error::custom("invalid service uri"))
}
