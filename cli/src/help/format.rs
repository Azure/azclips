use az_core::pb::{CmdArg, CmdInterface, ComponentMetadata};

use crate::output::StyledString;

use super::templates::{ArgTemplate, CmdTemplate, HardCodeArgTemplate, HardCodeCmdTemplate};

pub fn default_help_formatter() -> HelpFormatter<HardCodeCmdTemplate, HardCodeArgTemplate> {
    HelpFormatter {
        usage_template: HardCodeCmdTemplate { intent: 2 },
        arg_template: HardCodeArgTemplate { intent: 2 },
    }
}

pub struct HelpFormatter<CMD, ARG>
where
    CMD: CmdTemplate,
    ARG: ArgTemplate,
{
    pub usage_template: CMD,
    pub arg_template: ARG,
}

impl<CMD, ARG> HelpFormatter<CMD, ARG>
where
    CMD: CmdTemplate,
    ARG: ArgTemplate,
{
    pub fn format_cmd<'a>(
        &self,
        cmd: &CmdInterface,
        components: impl IntoIterator<Item = &'a ComponentMetadata>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        self.usage_template.render(cmd, components, term_width)
    }

    pub fn format_arg(&self, arg: &CmdArg, term_width: Option<usize>) -> Vec<StyledString> {
        self.arg_template.render(arg, term_width)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use az_core::pb::{cmd_interface, CmdArg, CmdArgHelp, CmdCommand, CmdHelp, CmdInterface};

    #[test]
    fn test_command_help_format() {
        let cmd = CmdInterface {
            level: Some(cmd_interface::Level::Command(CmdCommand {
                name: "command".to_string(),
                uri: "/group/command".to_string(),
                version: "1.0.0".to_string(),
                help: Some(CmdHelp {
                    short: "Short description".to_string(),
                    long: Some("Long description of the command. ".repeat(3)),
                }),
                args: vec![
                    CmdArg {
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
                    CmdArg {
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
            })),
        };

        let formatter = default_help_formatter();
        let formatted = formatter.format_cmd(&cmd, None, None);

        for line in &formatted {
            println!("{}", line);
        }
        assert!(!formatted.is_empty());
    }
}
