use az_core::pb::{CrsInterface, ComponentMetadata};

use crate::output::StyledString;

/// Trait for rendering command help templates.
///
/// We have the following properties for the command help template:
/// ## Service Properties
///
/// - `name`
/// - `uri`
/// - `level(enum)`
/// - `help/short`
/// - `help/long`
/// - `level/version`
/// - `level/groups(interface)`
/// - `level/commands(interface)`
///
/// ## Group Properties
///
/// - `name`
/// - `uri`
/// - `level(enum)`
/// - `help/short`
/// - `help/long`
/// - `level/groups(interface)`
/// - `level/commands(interface)`
///
/// ## Command Properties
///
/// - `name`
/// - `uri`
/// - `level(enum)`
/// - `help/short`
/// - `help/long`
/// - `level/version`
/// - `level/args(CrsArg)`
/// - `level/positional_args(string)`
/// - `level/args/var_name`
/// - `level/args/name`
/// - `level/args/options(string)`
/// - `level/args/group`
/// - `level/args/internal`
/// - `level/args/required`
/// - `level/args/nullable`
/// - `level/args/blank`
/// - `level/args/blank/value`
/// - `level/args/default`
/// - `level/args/default/value`
/// - `level/args/help(enum)`
/// - `level/args/help/short`
/// - `level/args/help/long`
/// - `level/args/prompt`
/// - `level/args/prompt/prompt`
/// - `level/args/prompt/secret`
/// - `level/args/prompt/confirm`
/// - `level/args/arg_type`
pub trait CmdTemplate {
    fn render<'a>(
        &self,
        cmd: &CrsInterface,
        components: impl IntoIterator<Item = &'a ComponentMetadata>,
        term_width: Option<usize>,
    ) -> Vec<StyledString>;
}
