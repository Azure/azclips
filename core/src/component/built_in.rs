use crate::{
    account::component::AccountComponent, cmd_uri::CmdUri, config::component::ConfigComponent,
    top::TopComponent,
};
use serde::{de::Error, ser::SerializeMap, Deserialize};
use std::{collections::HashMap, str::FromStr, sync::Arc};
use strum_macros::{Display, EnumIter};

use super::{center::AzComponentCenter, AzComponent, AzComponentCtx};

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

    pub fn uri(&self) -> CmdUri {
        match self {
            AzBuildIn::Top => TopComponent::uri(),
            AzBuildIn::Config => ConfigComponent::uri(),
            AzBuildIn::Account => AccountComponent::uri(),
            // AzBuildIn::Plugin => CmdUri::from_str(Self::PLUGIN_URI).unwrap(),
        }
    }

    pub fn from_uri(uri: &CmdUri) -> Option<Self> {
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
            .insert(AzBuildIn::Top, Arc::new(Box::new(TopComponent::default())));
        self.built_ins.insert(
            AzBuildIn::Config,
            Arc::new(Box::new(ConfigComponent::default())),
        );
        self.built_ins.insert(
            AzBuildIn::Account,
            Arc::new(Box::new(AccountComponent::default())),
        );
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
