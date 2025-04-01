use std::collections::HashMap;

use serde_json::Value;

use crate::{
    argparse::ArgValueAssignOp,
    pb::command::{self, CmdCommand},
    AzResult,
};

pub(crate) struct CmdParamBuilder {
    // the binary parameters will be stored as a file path
    params: HashMap<String, Value>,
    patch_keys: Vec<String>,
}

impl Default for CmdParamBuilder {
    fn default() -> Self {
        Self {
            params: HashMap::new(),
            patch_keys: Vec::new(),
        }
    }
}

impl CmdParamBuilder {
    pub fn apply(mut self, value_op: ArgValueAssignOp) -> Self {
        let mut keys = value_op.keys.parts();
        let k = keys.next().unwrap();

        // todo!()

        self
    }

    /// format the params based on the argument definition, error will be returned if the value is invalid
    ///
    /// The format process includes:
    /// 1. apply the default values
    /// 2. check values with format rules
    /// 3. parse the file path as binary data if the argument is a binary type string
    pub fn format_and_build(mut self, command: &CmdCommand) -> AzResult<command::CmdParams> {
        todo!()
    }
}
