use std::{collections::HashMap, str::FromStr};

use serde_json::Value;
use tracing::error;

use crate::{
    cmd_uri::CmdUri,
    component::AzComponentCtx,
    config::cloud::{endpoint::EndpointName, resource_id::ResourceIdName},
    error::AzError,
    pb::command::{
        cmd_arg, cmd_interface, cmd_string_arg, CmdArg, CmdArgHelp, CmdCommand, CmdHelp,
        CmdInterface, CmdParams, CmdStringArg, CmdStringFormat,
    },
    AzCloud, VERSION,
};

#[derive(Debug, Default)]
pub(super) struct UpdateCloud {
    name: AzCloud,
    endpoints: HashMap<EndpointName, String>,
    resource_ids: HashMap<ResourceIdName, String>,
    // internal property which will not exposed
    _patch_keys: Vec<String>,
}
