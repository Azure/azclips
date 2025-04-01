use crate::cmd_uri::CmdUri;
use serde::{de::Error, ser::SerializeMap, Deserialize};
use std::str::FromStr;
use strum_macros::{Display, EnumIter};

#[derive(Debug, Clone, PartialEq, Eq, Hash, EnumIter, Display)]
pub enum AzBuildIn {
    /// The component for the top level commands for the azure commands.
    Top,
    /// Manage the Account information.
    Account,
    /// Manage the registered Azure clouds.
    Cloud,
    /// Manage the configuration.
    Config,
    /// Manage the plugins.
    Plugin,
}

impl AzBuildIn {
    /// cmd_uri: `cmd://azure/`
    const TOP_URI: &'static str = "cmd://azure/";
    /// cmd_uri: `cmd://azure/account/`
    const ACCOUNT_URI: &'static str = "cmd://azure/account/";
    /// cmd_uri: `cmd://azure/cloud/`
    const CLOUD_URI: &'static str = "cmd://azure/cloud/";
    /// cmd_uri: `cmd://azure/config/`
    const CONFIG_URI: &'static str = "cmd://azure/config/";
    /// cmd_uri: `cmd://azure/plugin/`
    const PLUGIN_URI: &'static str = "cmd://azure/plugin/";

    pub fn uri(&self) -> CmdUri {
        match self {
            AzBuildIn::Top => CmdUri::from_str(Self::TOP_URI).unwrap(),
            AzBuildIn::Account => CmdUri::from_str(Self::ACCOUNT_URI).unwrap(),
            AzBuildIn::Cloud => CmdUri::from_str(Self::CLOUD_URI).unwrap(),
            AzBuildIn::Config => CmdUri::from_str(Self::CONFIG_URI).unwrap(),
            AzBuildIn::Plugin => CmdUri::from_str(Self::PLUGIN_URI).unwrap(),
        }
    }

    pub fn from_uri(uri: &CmdUri) -> Option<Self> {
        match uri.to_string().as_str() {
            s if s == Self::TOP_URI => Some(Self::Top),
            s if s == Self::ACCOUNT_URI => Some(Self::Account),
            s if s == Self::CLOUD_URI => Some(Self::Cloud),
            s if s == Self::CONFIG_URI => Some(Self::Config),
            s if s == Self::PLUGIN_URI => Some(Self::Plugin),
            _ => None,
        }
    }
}

pub(super) fn serialize_az_build_in_component<S>(
    component: &AzBuildIn,
    serializer: S,
) -> Result<S::Ok, S::Error>
where
    S: serde::Serializer,
{
    let mut map = serializer.serialize_map(Some(1))?;
    map.serialize_entry("uri", &component.uri().to_string())?;
    map.end()
}

pub(super) fn deserialize_az_build_in_component<'de, D>(
    deserializer: D,
) -> Result<AzBuildIn, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = serde_json::Value::deserialize(deserializer)?;
    let uri = value
        .get("uri")
        .and_then(|v| v.as_str())
        .ok_or_else(|| D::Error::custom("component uri is required"))?;
    let uri = CmdUri::from_str(uri).map_err(D::Error::custom)?;

    AzBuildIn::from_uri(&uri).ok_or_else(|| D::Error::custom("invalid component uri"))
}
