use az_core::pb::command::CmdArg;

use crate::output::StyledString;

pub trait ArgTemplate {
    fn render<'a>(&self, arg: &CmdArg, term_width: Option<usize>) -> Vec<StyledString>;
}