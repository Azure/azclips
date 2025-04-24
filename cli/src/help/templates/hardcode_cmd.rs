use std::collections::HashMap;

use az_core::pb::{command::{cmd_interface::Level, CmdCommandGroup, CmdComponent, CmdInterface}, component::ComponentMetadata};
use crossterm::style::Stylize;

use crate::{help::{table::HelpTableLine, MIN_DETAIL_WIDTH}, output::{Row, StyledString}};

use super::CmdTemplate;



pub struct HardCodeCmdTemplate {
    pub intent: usize,
}

impl Default for HardCodeCmdTemplate {
    fn default() -> Self {
        HardCodeCmdTemplate { intent: 2 }
    }
}

impl CmdTemplate for HardCodeCmdTemplate {
    fn render<'a>(
        &self,
        cmd: &CmdInterface,
        components: Option<Box<dyn Iterator<Item = &'a ComponentMetadata> + 'a>>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        let mut result = vec![];

        let header = match cmd.level {
            Some(Level::Command(_)) => "Command",
            Some(Level::Group(_)) => "Group",
            Some(Level::Component(_)) => "Service",
            None => "Azure",
        };
        let header_line = StyledString::from(header);
        result.push(header_line);

        let mut sig_row = Row::new();
        sig_row.append_sep(" ".repeat(self.intent));
        let uri_parts = cmd.uri.split('/').filter(|s| !s.is_empty()).skip(1);
        let cmd_sig = uri_parts.collect::<Vec<_>>().join(" ");
        sig_row.append_styled(
            vec![StyledString::from_content(&cmd_sig.clone().bold())],
            None,
        );
        if let Some(Level::Command(cmd)) = &cmd.level {
            if !cmd.positional_args.is_empty() {
                sig_row.append_one_line(" ");
                let p_args = cmd
                    .positional_args
                    .iter()
                    .map(|arg| format!("<{}>", arg))
                    .collect::<Vec<_>>()
                    .join(" ");
                sig_row.append_styled(vec![StyledString::from_content(&p_args.bold())], None);
            }
        }
        let version = match &cmd.level {
            Some(Level::Command(cmd)) => Some(cmd.version.clone()),
            Some(Level::Group(_)) => None,
            Some(Level::Component(cmd)) => Some(cmd.version.clone()),
            None => None,
        };
        if let Some(version) = &version {
            sig_row.append_one_line(" - ");
            sig_row.append_styled(
                vec![StyledString::from_content(&version.clone().stylize())],
                None,
            );
        }

        let short = cmd.help.as_ref().map(|help| &help.short);
        if let Some(short) = short {
            sig_row.append_one_line(" : ");
            let short_help = StyledString::from_content(&short.clone().italic());
            let short_help_lines = short_help.split_lines();
            let version_len = version.as_ref().map(|v| v.len() + 3).unwrap_or(0);
            let width = term_width
                .map(|tw| tw.saturating_sub(self.intent + cmd_sig.len() + version_len + 3))
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            sig_row.append_styled(short_help_lines, width);
        };
        result.append(&mut sig_row.render());

        if let Some(long) = cmd.help.as_ref().and_then(|help| help.long.as_ref()) {
            let mut long_help_row = Row::new();
            long_help_row.append_sep(" ".repeat(self.intent * 2));
            let long_help = StyledString::from(long);
            let long_help_lines = long_help.split_lines();
            let width = term_width
                .map(|tw| tw.saturating_sub(self.intent * 2))
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            long_help_row.append_styled(long_help_lines, width);
            result.append(&mut long_help_row.render());
        }

        result.push(StyledString::from(""));

        let mut help_table_groups = HashMap::new();
        match &cmd.level {
            Some(Level::Command(cmd)) => {
                for arg in cmd.args.iter() {
                    let short = arg
                        .options
                        .iter()
                        .filter(|o| o.len() == 1)
                        .map(|s| format!("-{}", s).bold())
                        .map(|s| StyledString::from_vec(vec![s]))
                        .collect::<Vec<_>>();
                    let args = if arg.options.is_empty() {
                        vec![StyledString::from(format!("<{}>", arg.var_name))]
                    } else {
                        arg.options
                            .iter()
                            .filter(|o| o.len() != 1)
                            .map(|s| format!("--{}", s).bold())
                            .map(|s| StyledString::from_vec(vec![s]))
                            .collect::<Vec<_>>()
                    };
                    let mut tags = vec![];
                    if arg.required {
                        tags.push(StyledString::from_content(&"[Required]".yellow()));
                    }
                    let mut description = vec![];
                    if let Some(help) = &arg.help {
                        let styled_help: StyledString = help.short.clone().into();
                        description.append(&mut styled_help.split_lines());
                    }
                    if let Some(default) = &arg.default {
                        let value = default.value.clone().blue().on_yellow();
                        let styled_default =
                            StyledString::from_vec(vec!["Default: ".to_owned().stylize(), value]);
                        description.push(styled_default);
                    }
                    let line = HelpTableLine {
                        short,
                        name: args,
                        tags,
                        description,
                        detail: vec![],
                    };
                    let group = help_table_groups
                        .entry(arg.group.clone())
                        .or_insert_with(|| vec![]);
                    group.push(line);
                }
            }
            Some(Level::Group(CmdCommandGroup { commands, groups }))
            | Some(Level::Component(CmdComponent {
                version: _,
                commands,
                groups,
            })) => {
                let command_rows = commands
                    .iter()
                    .map(|cmd| HelpTableLine {
                        short: vec![],
                        name: vec![StyledString::from_content(&cmd.name.clone().bold())],
                        tags: vec![],
                        description: cmd
                            .help
                            .as_ref()
                            .map(|help| &help.short)
                            .map(|desc| {
                                StyledString::from_content(&desc.clone().italic()).split_lines()
                            })
                            .unwrap_or(vec![]),
                        detail: vec![],
                    })
                    .collect::<Vec<_>>();
                if !command_rows.is_empty() {
                    help_table_groups.insert("Command".to_string(), command_rows);
                }
                let group_rows = groups
                    .iter()
                    .map(|group| HelpTableLine {
                        short: vec![],
                        name: vec![StyledString::from_content(&group.name.clone().bold())],
                        tags: vec![],
                        description: group
                            .help
                            .as_ref()
                            .map(|help| &help.short)
                            .map(|desc| {
                                StyledString::from_content(&desc.clone().italic()).split_lines()
                            })
                            .unwrap_or(vec![]),
                        detail: vec![],
                    })
                    .collect::<Vec<_>>();
                if !group_rows.is_empty() {
                    help_table_groups.insert("Command Group".to_string(), group_rows);
                }
                if let Some(components) = components {
                    let component_rows = components
                        .map(|component| HelpTableLine {
                            short: vec![],
                            name: vec![StyledString::from_content(&component.name.clone().bold())],
                            tags: vec![],
                            description: component
                                .help
                                .as_ref()
                                .map(|help| &help.short)
                                .map(|desc| {
                                    StyledString::from_content(&desc.clone().italic()).split_lines()
                                })
                                .unwrap_or(vec![]),
                            detail: vec![],
                        })
                        .collect::<Vec<_>>();
                    if !component_rows.is_empty() {
                        help_table_groups.insert("Service".to_string(), component_rows);
                    }
                }
            }
            None => {}
        }
        result.append(&mut HelpTableLine::render_groups(
            &help_table_groups,
            term_width,
            self.intent,
        ));
        result
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use az_core::pb::command::{CmdArgHelp, CmdHelp, CmdInterface};

    #[test]
    fn test_help_cmd_template_render() {
        let cmd = CmdInterface {
            name: "command".to_string(),
            uri: "cmd://azure/group/command".to_string(),
            level: Some(az_core::pb::command::cmd_interface::Level::Command(
                az_core::pb::command::CmdCommand {
                    version: "1.0.0".to_string(),
                    args: vec![
                        az_core::pb::command::CmdArg {
                            var_name: "arg1".to_string(),
                            options: vec![
                                "a".to_string(),
                                "arg1".to_string(),
                                "arg1option".to_string(),
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
                            options: vec!["b".to_string(), "arg2".to_string()],
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

        let template = HardCodeCmdTemplate { intent: 2 };

        let result = template.render(&cmd, None, Some(80));

        for line in &result {
            println!("{}", line);
        }

        assert_eq!(result.len(), 9); // header, body, detail
        assert_eq!(result[0].to_string(), "Command"); // Header
        assert!(result[1].to_string().contains("azure group command")); // Body
        assert!(result[1].to_string().contains("Short description")); // Body short description
        assert!(result[1].to_string().starts_with("  ")); // Indent for body
        assert!(result[2]
            .to_string()
            .contains("Long description of the command.")); // Detail
        assert!(result[2].to_string().starts_with("    ")); // Indent for detail

        assert_eq!(result[5].to_string(), "Group 1");
        assert!(result[6].to_string().contains("-a"));
        assert!(result[6].to_string().contains("--arg1"));
        assert!(result[7].to_string().contains("--arg1option"));
    }
}