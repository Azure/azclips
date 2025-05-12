use std::{collections::HashMap, str::FromStr};

use serde_json::Value;
use tracing::error;

use crate::{
    cmd_uri::CmdUri,
    component::AzComponentCtx,
    error::AzError,
    pb::command::{
        cmd_arg, cmd_interface, cmd_string_arg, CmdArg, CmdArgHelp, CmdCommand, CmdHelp,
        CmdInterface, CmdParams, CmdStringArg, CmdStringFormat,
    },
    VERSION,
};

#[derive(Debug, Default)]
pub(super) struct CreateCloud {
    name: String,
    endpoints: HashMap<String, String>,
    resource_ids: HashMap<String, String>,
}

impl CreateCloud {}
