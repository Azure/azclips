use az_core::pb::{command::CmdInterface, component::ComponentMetadata};

use crate::output::StyledString;

pub trait CmdTemplate {
    fn render<'a>(
        &self,
        cmd: &CmdInterface,
        components: Option<Box<dyn Iterator<Item = &'a ComponentMetadata> + 'a>>,
        term_width: Option<usize>,
    ) -> Vec<StyledString>;
}