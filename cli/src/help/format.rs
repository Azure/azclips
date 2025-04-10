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

#[cfg(test)]
mod tests {
    use az_core::pb::command::{CmdArgHelp, CmdHelp, CmdInterface};
    use super::*;

    use crate::help::ApplicationDetail;

    #[test]
    fn test_command_help_format() {
        let cmd = CmdInterface {
            name: "command".to_string(),
            uri: "/group/command".to_string(),
            level: Some(az_core::pb::command::cmd_interface::Level::Command(
                az_core::pb::command::CmdCommand {
                    version: "1.0.0".to_string(),
                    args: vec![
                        az_core::pb::command::CmdArg {
                            var_name: "arg1".to_string(),
                            options: vec![
                                "-a".to_string(),
                                "--arg1".to_string(),
                                "--arg1option".to_string(),
                            ],
                            name: "Argument 1".to_string(),
                            group: "Group 1".to_string(),
                            internal: false,
                            required: true,
                            default: None,
                            help: Some(CmdArgHelp {
                                short: "Short description".to_string(),
                                long: Some("Long description of the argument.".to_string()),
                            }),
                            nullable: false,
                            blank: None,
                            prompt: None,
                            arg_type: None,
                        },
                        az_core::pb::command::CmdArg {
                            var_name: "arg2".to_string(),
                            options: vec!["-b".to_string(), "--arg2".to_string()],
                            name: "Argument 2".to_string(),
                            group: "Group 1".to_string(),
                            internal: false,
                            required: false,
                            default: None,
                            help: Some(CmdArgHelp {
                                short: "Short description".to_string(),
                                long: Some("Long description of the argument.".to_string()),
                            }),
                            nullable: false,
                            blank: None,
                            prompt: None,
                            arg_type: None,
                        },
                    ],
                    positional_args: vec![],
                },
            )),
            help: Some(CmdHelp {
                short: "Short description".to_string(),
                long: Some("Long description of the command. ".repeat(3)),
            }),
        };

        let app = ApplicationDetail {
            executable: "az".to_string(),
            version: "1.0.0".to_string(),
        };

        let formatter = HelpFormatter::default();
        let formatted = formatter.format(&cmd, &app, None, None);

        for line in &formatted {
            println!("{}", line);
        }
        assert!(!formatted.is_empty());
    }
}
