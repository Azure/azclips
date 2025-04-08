use serde_json::{
    map::{Entry, OccupiedEntry, VacantEntry},
    Map, Value,
};

use crate::{
    argparse::{partial::SHPartialKeyPart, ArgValueAssignOp},
    pb::command::{self, CmdCommand},
    AzResult,
};

enum ParamEntry<'a> {
    Occupied(ParamOccupiedEntry<'a>),
    Vacant(ParamVacantEntry<'a>),
}

enum ParamOccupiedEntry<'a> {
    Map(OccupiedEntry<'a>),
}

enum ParamVacantEntry<'a> {
    Map(VacantEntry<'a>),
}

pub(crate) struct CmdParamBuilder {
    // the binary parameters will be stored as a file path in the value and converted to a binary data when building the command
    params: Value,
    patch_keys: Vec<String>,
}

impl Default for CmdParamBuilder {
    fn default() -> Self {
        Self {
            params: Value::Object(Map::new()),
            patch_keys: Vec::new(),
        }
    }
}

impl CmdParamBuilder {
    pub fn apply(mut self, value_op: ArgValueAssignOp) -> Self {
        let mut keys = value_op.keys.parts();

        let mut cur_value = self.params.as_object_mut().unwrap();
        let key = keys.next().unwrap().as_name().unwrap();

        // while let Some(next_key) = keys.next() {}

        // let mut keys = value_op.keys.parts();
        // let k = keys.next().unwrap();

        // let arg_name = match k {
        //     SHPartialKeyPart::Name(name) => name.to_string(),
        //     SHPartialKeyPart::Index(_) => {
        //         unreachable!("not available using index to access the argument")
        //     }
        //     SHPartialKeyPart::AppendIndex(_) => {
        //         unreachable!("not available using index to access the argument")
        //     }
        // };

        // let mut entry = self.params.entry(arg_name);
        // while let Some(k) = keys.next() {
        //     // let k = k.to_string();
        //     // entry = entry.or_insert(Value::Object(Map::new()));
        // }

        // // let param = self.params.entry(arg_name)

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
