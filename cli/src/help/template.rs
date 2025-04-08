use std::{collections::HashMap, num::NonZeroUsize};

use az_core::pb::command::CmdInterface;

#[derive(Debug, Clone)]
pub struct HelpTemplate {
    header: Option<String>,
    sections: HashMap<String, HelpSectionTemplate>,
    tail: Option<String>,
    term_width: Option<NonZeroUsize>,
}

// impl HelpTemplate {
//     pub fn render(&self, help: &CmdInterface)
// }

#[derive(Debug, Clone)]
struct HelpSectionTemplate {
    header: Option<String>,
    line: String,
    tail: Option<String>,
    term_width: Option<NonZeroUsize>,
}
