use std::collections::HashMap;

use az_core::pb::{
    cmd_interface::Level, CmdCommandGroup, CmdComponent, CmdInterface, ComponentMetadata,
};

use crate::{
    help::{
        table::{CmdArgDisplayConfig, HelpLineDisplay, HelpTableLine},
        MIN_DETAIL_WIDTH,
    },
    output::{style::Stylize, Row, StyledString},
};

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
        components: impl IntoIterator<Item = &'a ComponentMetadata>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        let mut result = vec![];

        let header = match cmd.level {
            Some(Level::Command(_)) => "Command",
            Some(Level::CommandGroup(_)) => "Group",
            Some(Level::Component(_)) => "Service",
            None => "Azure",
        };
        let header_line = StyledString::from(header);
        result.push(header_line);

        let mut sig_row = Row::new();
        sig_row.append_sep(" ".repeat(self.intent));
        let uri_parts = cmd
            .get_uri()
            .unwrap()
            .split('/')
            .filter(|s| !s.is_empty())
            .skip(1);
        let cmd_sig = uri_parts.collect::<Vec<_>>().join(" ");
        sig_row.append_styled(vec![cmd_sig.bold().into()], None);
        if let Some(Level::Command(cmd)) = &cmd.level {
            if !cmd.positional_args.is_empty() {
                sig_row.append_one_line(" ");
                let p_args = cmd
                    .positional_args
                    .iter()
                    .map(|arg| format!("<{}>", arg))
                    .collect::<Vec<_>>()
                    .join(" ");
                sig_row.append_styled(vec![p_args.bold().into()], None);
            }
        }
        let version = match &cmd.level {
            Some(Level::Command(cmd)) => Some(cmd.version.clone()),
            Some(Level::CommandGroup(_)) => None,
            Some(Level::Component(cmd)) => Some(cmd.version.clone()),
            None => None,
        };
        if let Some(version) = version {
            sig_row.append_one_line(" - ");
            sig_row.append_styled(vec![version.stylize().into()], None);
        }

        let short = cmd.get_help().map(|help| &help.short);
        if let Some(short) = short {
            sig_row.append_one_line(" : ");
            let short_help: StyledString = short.clone().italic().into();
            let short_help_lines = short_help.split_lines();
            let width = term_width
                .map(|tw| tw.saturating_sub(sig_row.term_len()))
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            sig_row.append_styled(short_help_lines, width);
        };
        result.append(&mut sig_row.render());

        if let Some(long) = cmd.get_help().and_then(|help| help.long.as_ref()) {
            let mut long_help_row = Row::new();
            long_help_row.append_sep(" ".repeat(self.intent * 2));
            let long_help: StyledString = long.into();
            let long_help_lines = long_help.split_lines();
            let width = term_width
                .map(|tw| tw.saturating_sub(self.intent * 2))
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            long_help_row.append_styled(long_help_lines, width);
            result.append(&mut long_help_row.render());
        }

        result.push("".into());

        let mut help_table_groups = HashMap::new();
        match &cmd.level {
            Some(Level::Command(cmd)) => {
                for arg in cmd.args.iter() {
                    let line = arg.help_line(CmdArgDisplayConfig {
                        show_options: true,
                        positions: Some(&cmd.positional_args[..]),
                    });
                    let group = help_table_groups
                        .entry(arg.group.clone())
                        .or_insert_with(|| vec![]);
                    group.push(line);
                }
            }
            Some(Level::CommandGroup(CmdCommandGroup {
                commands, groups, ..
            }))
            | Some(Level::Component(CmdComponent {
                commands, groups, ..
            })) => {
                let command_rows = commands
                    .iter()
                    .map(|cmd| cmd.help_line(()))
                    .collect::<Vec<_>>();
                if !command_rows.is_empty() {
                    help_table_groups.insert("Command".to_string(), command_rows);
                }
                let group_rows = groups
                    .iter()
                    .map(|group| group.help_line(()))
                    .collect::<Vec<_>>();
                if !group_rows.is_empty() {
                    help_table_groups.insert("Command Group".to_string(), group_rows);
                }
                let component_rows = components
                    .into_iter()
                    .map(|component| component.help_line(()))
                    .collect::<Vec<_>>();
                if !component_rows.is_empty() {
                    help_table_groups.insert("Service".to_string(), component_rows);
                }
            }
            None => {}
        }
        result.append(&mut HelpTableLine::render_groups(
            help_table_groups,
            term_width,
            self.intent,
        ));
        result
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use az_core::pb::{cmd_interface, CmdArg, CmdArgHelp, CmdCommand, CmdHelp, CmdInterface};

    #[test]
    fn test_help_cmd_template_render() {
        let cmd = CmdInterface {
            level: Some(cmd_interface::Level::Command(CmdCommand {
                name: "command".to_string(),
                uri: "cmd://azure/group/command".to_string(),
                version: "1.0.0".to_string(),
                help: Some(CmdHelp {
                    short: "Short description".to_string(),
                    long: Some("Long description of the command. ".repeat(3)),
                }),
                args: vec![
                    CmdArg {
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
                    CmdArg {
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
            })),
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
