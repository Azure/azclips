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
    const BASE_SEGMENTS: &'static [&'static str] = &["azure"];
    /// cmd_uri: `cmd://azure/account/`
    const ACCOUNT_SEGMENTS: &'static [&'static str] = &["azure", "account"];
    /// cmd_uri: `cmd://azure/cloud/`
    const CLOUD_SEGMENTS: &'static [&'static str] = &["azure", "cloud"];
    /// cmd_uri: `cmd://azure/config/`
    const CONFIG_SEGMENTS: &'static [&'static str] = &["azure", "config"];
    /// cmd_uri: `cmd://azure/plugin/`
    const PLUGIN_SEGMENTS: &'static [&'static str] = &["azure", "plugin"];

    pub fn uri(&self) -> CmdUri {
        CmdUri::new(self.segments().to_vec(), false)
    }

    pub fn segments(&self) -> &'static [&'static str] {
        match self {
            AzBuildInService::Base => Self::BASE_SEGMENTS,
            AzBuildInService::Account => Self::ACCOUNT_SEGMENTS,
            AzBuildInService::Cloud => Self::CLOUD_SEGMENTS,
            AzBuildInService::Config => Self::CONFIG_SEGMENTS,
            AzBuildInService::Plugin => Self::PLUGIN_SEGMENTS,
        }
    }

    pub fn from_segments(segments: &[&str]) -> Option<Self> {
        match segments {
            s if s == Self::BASE_SEGMENTS => Some(Self::Base),
            s if s == Self::ACCOUNT_SEGMENTS => Some(Self::Account),
            s if s == Self::CLOUD_SEGMENTS => Some(Self::Cloud),
            s if s == Self::CONFIG_SEGMENTS => Some(Self::Config),
            s if s == Self::PLUGIN_SEGMENTS => Some(Self::Plugin),
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

    AzBuildInService::from_segments(&uri.segments().collect::<Vec<_>>())
        .ok_or_else(|| D::Error::custom("invalid service uri"))
}
