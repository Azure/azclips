use az_core::pb::{command::CmdInterface, component::ComponentMetadata};

use crate::output::StyledString;

use super::{template::{HelpArgTemplate, HelpCmdTemplate, HelpUsageTemplate}, ApplicationDetail};

pub fn default_help_formatter() -> HelpFormatter {
    HelpFormatter {
        usage_template: HelpUsageTemplate::default(),
        cmd_template: HelpCmdTemplate::default(),
        arg_template: HelpArgTemplate::default(),
    }
}

pub struct HelpFormatter {
    pub usage_template: HelpUsageTemplate,
    pub cmd_template: HelpCmdTemplate,
    pub arg_template: HelpArgTemplate,
}

impl Default for HelpFormatter {
    fn default() -> Self {
        default_help_formatter()
    }
}

impl HelpFormatter {
    // pub fn format(&self) -> AzResult<String> {
    pub fn format<'a>(
        &self,
        cmd: &CmdInterface,
        app: &ApplicationDetail,
        components: Option<Box<dyn Iterator<Item = &'a ComponentMetadata> + 'a>>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        let mut result = vec![];
        result.extend(self.usage_template.render(cmd, app, term_width));

        let body = match &cmd.level {
            Some(az_core::pb::command::cmd_interface::Level::Command(command)) => {
                self.arg_template.render(&command, term_width)
            },
            Some(az_core::pb::command::cmd_interface::Level::Group(group)) => {
                self.cmd_template.render(&group.groups, &group.commands, components, term_width)
            },
            Some(az_core::pb::command::cmd_interface::Level::Component(component)) => {
                self.cmd_template.render(&component.groups, &component.commands, components, term_width)
            },
            None => {
                vec![]
            }
        };
        if !body.is_empty() {
            result.push(StyledString::from(""));
        }
        result.extend(body);
        result
    }
}
