use az_core::pb::{ComponentMetadata, CrsArg, CrsInterface};

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
        cmd: &CrsInterface,
        components: impl IntoIterator<Item = &'a ComponentMetadata>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        self.usage_template.render(cmd, components, term_width)
    }

    pub fn format_arg(&self, arg: &CrsArg, term_width: Option<usize>) -> Vec<StyledString> {
        self.arg_template.render(arg, term_width)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use az_core::pb::{crs_interface, CrsArg, CrsArgHelp, CrsCommand, CrsHelp, CrsInterface};

    #[test]
    fn test_command_help_format() {
        let cmd = CrsInterface {
            level: Some(crs_interface::Level::Command(CrsCommand {
                name: "command".to_string(),
                uri: "/group/command".to_string(),
                version: "1.0.0".to_string(),
                help: Some(CrsHelp {
                    short: "Short description".to_string(),
                    long: Some("Long description of the command. ".repeat(3)),
                }),
                args: vec![
                    CrsArg {
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
                        help: Some(CrsArgHelp {
                            short: "Short description".to_string(),
                            long: Some("Long description of the argument.".to_string()),
                        }),
                        nullable: false,
                        blank: None,
                        prompt: None,
                        arg_type: None,
                    },
                    CrsArg {
                        var_name: "arg2".to_string(),
                        options: vec!["-b".to_string(), "--arg2".to_string()],
                        name: "Argument 2".to_string(),
                        group: "Group 1".to_string(),
                        internal: false,
                        required: false,
                        default: None,
                        help: Some(CrsArgHelp {
                            short: "Short description".to_string(),
                            long: Some("Long description of the argument.".to_string()),
                        }),
                        nullable: false,
                        blank: None,
                        prompt: None,
                        arg_type: None,
                    },
                ],
                ..Default::default()
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
