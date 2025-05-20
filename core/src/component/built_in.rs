use crate::{
    account::component::AccountComponent, cmd_uri::CmdUri, config::component::ConfigComponent,
    top::component::TopComponent,
};
use serde::{de::Error, ser::SerializeMap, Deserialize, Deserializer, Serialize, Serializer};
use std::{str::FromStr, sync::Arc};
use strum_macros::{Display, EnumIter};

use super::{center::AzComponentCenter, cmd::AzCommandGroup, AzComponentCtx};

#[derive(Debug, Clone, PartialEq, Eq, Hash, EnumIter, Display)]
pub enum AzBuildIn {
    /// The component for the top level commands for the azure commands.
    Top,
    /// Manage the configuration.
    Config,
    /// Manage the Account information.
    Account,
    // /// Manage the plugins.
    // Plugin,
}

impl AzBuildIn {
    // /// cmd_uri: `cmd://azure/plugin/`
    // const PLUGIN_URI: &'static str = "cmd://azure/plugin/";

    pub(crate) fn uri(&self) -> CmdUri {
        match self {
            AzBuildIn::Top => TopComponent::uri(),
            AzBuildIn::Config => ConfigComponent::uri(),
            AzBuildIn::Account => AccountComponent::uri(),
            // AzBuildIn::Plugin => CmdUri::from_str(Self::PLUGIN_URI).unwrap(),
        }
    }

    pub(crate) fn from_uri(uri: &CmdUri) -> Option<Self> {
        match uri.to_string().as_str() {
            s if s == TopComponent::URI => Some(Self::Top),
            s if s == ConfigComponent::URI => Some(Self::Config),
            s if s == AccountComponent::URI => Some(Self::Account),
            // s if s == Self::PLUGIN_URI => Some(Self::Plugin),
            _ => None,
        }
    }
}

impl<T: AzComponentCtx> AzComponentCenter<T> {
    pub(super) fn load_built_in_components(&mut self) {
        self.built_ins
            .insert(AzBuildIn::Top, Arc::new(TopComponent::default()));
        self.built_ins
            .insert(AzBuildIn::Config, Arc::new(ConfigComponent::default()));
        self.built_ins
            .insert(AzBuildIn::Account, Arc::new(AccountComponent::default()));
    }
}

impl Serialize for AzBuildIn {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut map = serializer.serialize_map(Some(1))?;
        map.serialize_entry("uri", &self.uri().to_string())?;
        map.end()
    }
}

impl<'de> Deserialize<'de> for AzBuildIn {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let value = serde_json::Value::deserialize(deserializer)?;
        let uri = value
            .get("uri")
            .and_then(|v| v.as_str())
            .ok_or_else(|| D::Error::custom("component uri is required"))?;
        let uri = CmdUri::from_str(uri).map_err(D::Error::custom)?;
        AzBuildIn::from_uri(&uri).ok_or_else(|| D::Error::custom("invalid component uri"))
    }
}
