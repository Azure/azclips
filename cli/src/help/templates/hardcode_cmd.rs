use std::collections::HashMap;

use az_core::pb::{crs_interface::Level, ComponentMetadata, CrsCommandGroup, CrsInterface};

use crate::{
    help::{
        table::{CrsArgDisplayConfig, HelpLineDisplay, HelpTableLine},
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
        cmd: &CrsInterface,
        components: impl IntoIterator<Item = &'a ComponentMetadata>,
        term_width: Option<usize>,
    ) -> Vec<StyledString> {
        let mut result = vec![];

        let header = match cmd.level {
            Some(Level::Command(_)) => "Command",
            Some(Level::CommandGroup(_)) => "Group",
            None => "Azure",
        };
        let header_line = StyledString::from(header);
        result.push(header_line);

        let mut sig_row = Row::new();
        sig_row.append_sep(" ".repeat(self.intent));
        let uri_parts = cmd.uri().split('/').filter(|s| !s.is_empty()).skip(1);
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
            None => None,
        };
        if let Some(version) = version {
            sig_row.append_one_line(" - ");
            sig_row.append_styled(vec![version.stylize().into()], None);
        }

        let short = cmd.help().map(|help| &help.short);
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

        if let Some(long) = cmd.help().and_then(|help| help.long.as_ref()) {
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
                let mut sorted_args = cmd.args.clone();
                sorted_args.sort_by_key(|arg| (arg.group.clone(), arg.name.clone()));
                
                for arg in sorted_args.iter() {
                    let line = arg.help_line(CrsArgDisplayConfig {
                        prepend_dash: true,
                        positions: Some(&cmd.positional_args[..]),
                    });
                    let group = help_table_groups
                        .entry(arg.group.clone())
                        .or_insert_with(|| vec![]);
                    group.push(line);
                }
            }
            Some(Level::CommandGroup(CrsCommandGroup {
                commands, groups, ..
            })) => {
                let mut sorted_commands = commands.clone();
                sorted_commands.sort_by_key(|cmd| cmd.name.clone());
                
                let command_rows = sorted_commands
                    .iter()
                    .map(|cmd| cmd.help_line(()))
                    .collect::<Vec<_>>();
                if !command_rows.is_empty() {
                    help_table_groups.insert("Command".to_string(), command_rows);
                }
                
                let mut sorted_groups = groups.clone();
                sorted_groups.sort_by_key(|group| group.name.clone());
                
                let group_rows = sorted_groups
                    .iter()
                    .map(|group| group.help_line(()))
                    .collect::<Vec<_>>();
                if !group_rows.is_empty() {
                    help_table_groups.insert("Command Group".to_string(), group_rows);
                }
                
                let mut component_vec: Vec<_> = components.into_iter().collect();
                component_vec.sort_by_key(|component| component.name.clone());
                
                let component_rows = component_vec
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
    use az_core::pb::{crs_interface, CrsArg, CrsArgHelp, CrsCommand, CrsHelp, CrsInterface};

    #[test]
    fn test_help_cmd_template_render() {
        let cmd = CrsInterface {
            level: Some(crs_interface::Level::Command(CrsCommand {
                name: "command".to_string(),
                uri: "crs://azure/group/command".to_string(),
                version: "1.0.0".to_string(),
                help: Some(CrsHelp {
                    short: "Short description".to_string(),
                    long: Some("Long description of the command. ".repeat(3)),
                }),
                args: vec![
                    CrsArg {
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
                        options: vec!["b".to_string(), "arg2".to_string()],
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
