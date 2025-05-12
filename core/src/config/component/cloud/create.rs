use std::{collections::HashMap, str::FromStr};

use serde_json::Value;
use tracing::error;

use crate::{
    cmd_uri::CmdUri,
    component::AzComponentCtx,
    config::cloud::{endpoint::EndpointMap, resource_id::ResourceIdMap},
    error::AzError,
    pb::command::{
        cmd_arg, cmd_interface, cmd_string_arg, CmdArg, CmdArgHelp, CmdCommand, CmdHelp,
        CmdInterface, CmdParams, CmdStringArg, CmdStringFormat,
    },
    AzCloud, VERSION,
};

#[derive(Debug, Default)]
pub(super) struct CreateCloud {
    name: AzCloud,
    endpoints: EndpointMap,
    resource_ids: ResourceIdMap,
}

// impl CreateCloud {}
