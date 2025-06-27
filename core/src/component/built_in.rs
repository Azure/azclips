use crate::{
    account::component::AccountComponent, config::component::ConfigComponent,
    plugin::component::PluginComponent, top::component::TopComponent, uri::CrsUri,
};
use serde::{de::Error, ser::SerializeMap, Deserialize, Deserializer, Serialize, Serializer};
use std::{str::FromStr, sync::Arc};
use strum_macros::{Display, EnumIter};

use super::{center::AzComponentCenter, AzCommandGroup, AzComponentCtx};

#[derive(Debug, Clone, PartialEq, Eq, Hash, EnumIter, Display)]
pub enum AzBuiltIn {
    /// The component for the top level commands for the azure commands.
    Top,
    /// Manage the configuration.
    Config,
    /// Manage the Account information.
    Account,
    /// Manage the plugins.
    Plugin,
}

impl AzBuiltIn {
    pub fn uri(&self) -> CrsUri {
        match self {
            AzBuiltIn::Top => TopComponent::uri(),
            AzBuiltIn::Config => ConfigComponent::uri(),
            AzBuiltIn::Account => AccountComponent::uri(),
            AzBuiltIn::Plugin => PluginComponent::uri(),
        }
    }

    pub fn from_uri(uri: &CrsUri) -> Option<Self> {
        match uri.to_string().as_str() {
            s if s == TopComponent::URI => Some(Self::Top),
            s if s == ConfigComponent::URI => Some(Self::Config),
            s if s == AccountComponent::URI => Some(Self::Account),
            s if s == PluginComponent::URI => Some(Self::Plugin),
            _ => None,
        }
    }
}

impl<T: AzComponentCtx> AzComponentCenter<T> {
    pub(super) fn load_built_in_components(&mut self) {
        self.built_ins
            .insert(AzBuiltIn::Top, Arc::new(TopComponent::default()));
        self.built_ins
            .insert(AzBuiltIn::Config, Arc::new(ConfigComponent::default()));
        self.built_ins
            .insert(AzBuiltIn::Account, Arc::new(AccountComponent::default()));
        self.built_ins
            .insert(AzBuiltIn::Plugin, Arc::new(PluginComponent::default()));
    }
}

impl Serialize for AzBuiltIn {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut map = serializer.serialize_map(Some(1))?;
        map.serialize_entry("uri", &self.uri().to_string())?;
        map.end()
    }
}

impl<'de> Deserialize<'de> for AzBuiltIn {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let value = serde_json::Value::deserialize(deserializer)?;
        let uri = value
            .get("uri")
            .and_then(|v| v.as_str())
            .ok_or_else(|| D::Error::custom("component uri is required"))?;
        let uri = CrsUri::from_str(uri).map_err(D::Error::custom)?;
        AzBuiltIn::from_uri(&uri).ok_or_else(|| D::Error::custom("invalid component uri"))
    }
}
