use std::collections::HashMap;

use az_core::pb::{
    command::{
        cmd_arg::ArgType, cmd_interface::Level, cmd_number_arg, cmd_string_arg, CmdArg,
        CmdCommandGroup, CmdComponent, CmdInterface, CmdNumberArg, CmdStringArg,
    },
    component::ComponentMetadata,
};
use crossterm::style::Stylize;

use crate::output::{Row, RowCell, StyledString};

/// Minimum width for the terminal
/// This is used after considering the intent
const MIN_TERM_WIDTH: usize = 20;

struct HelpTableLine {
    short: Vec<StyledString>,
    name: Vec<StyledString>,
    tags: Vec<StyledString>,
    description: Vec<StyledString>,
    detail: Vec<StyledString>,
}

impl Default for HelpTableLine {
    fn default() -> Self {
        HelpTableLine {
            short: vec![],
            name: vec![],
            tags: vec![],
            description: vec![],
            detail: vec![],
        }
    }
}

impl HelpTableLine {
    fn calc_width(cell: &Vec<StyledString>) -> usize {
        cell.iter().map(|s| s.term_len()).max().unwrap_or(0)
    }

    fn render_table(
        table: &Vec<Self>,
        term_width: Option<usize>,
        offset: usize,
        intent: usize,
    ) -> Vec<StyledString> {
        let short_width = table
            .iter()
            .map(|line| Self::calc_width(&line.short))
            .max()
            .unwrap_or(0);
        let option_width = table
            .iter()
            .map(|line| Self::calc_width(&line.name))
            .max()
            .unwrap_or(0);
        let tag_width = table
            .iter()
            .map(|line| Self::calc_width(&line.tags))
            .max()
            .unwrap_or(0);
        let mut result = vec![];
        for line in table {
            let mut row = Row::new();
            row.append_sep(" ".repeat(offset));
            if short_width > 0 {
                let short_cell = RowCell::from_styled(line.short.clone(), Some(short_width));
                row.append(short_cell);
                row.append_sep(" ");
            }
            if option_width > 0 {
                let option_cell = RowCell::from_styled(line.name.clone(), Some(option_width));
                row.append(option_cell);
                row.append_sep(" ");
            }
            if tag_width > 0 {
                let tag_cell = RowCell::from_styled(line.tags.clone(), Some(tag_width));
                row.append(tag_cell);
            }
            if !line.description.is_empty() {
                row.append_one_line(": ");
                let description_cell = RowCell::from_styled(line.description.clone(), None);
                row.append(description_cell);
            }
            result.append(&mut row.render());
            let mut detail_row = Row::new();
            detail_row.append_sep(" ".repeat(offset + intent));
            let detail_width = term_width
                .map(|tw| tw.saturating_sub(offset + intent * 2))
                .filter(|w| *w > MIN_TERM_WIDTH);
            let detail_cell = RowCell::from_styled(line.detail.clone(), detail_width);
            detail_row.append(detail_cell);
            result.append(&mut detail_row.render());
        }
        result
    }

    fn render_groups(
        groups: &HashMap<String, Vec<Self>>,
        term_width: Option<usize>,
        intent: usize,
    ) -> Vec<StyledString> {
        let short_width = groups
            .values()
            .flat_map(|group| group.iter())
            .map(|line| Self::calc_width(&line.short))
            .max()
            .unwrap_or(0);
        let option_width = groups
            .values()
            .flat_map(|group| group.iter())
            .map(|line| Self::calc_width(&line.name))
            .max()
            .unwrap_or(0);
        let tag_width = groups
            .values()
            .flat_map(|group| group.iter())
            .map(|line| Self::calc_width(&line.tags))
            .max()
            .unwrap_or(0);
        let mut result = vec![];
        for (group_name, group) in groups {
            let group_header = StyledString::from(if group_name.is_empty() {
                "Default".to_owned()
            } else {
                format!("{}", group_name)
            });
            result.push(group_header);
            for line in group {
                let mut row = Row::new();
                row.append_sep(" ".repeat(intent));
                if short_width > 0 {
                    let short_cell = RowCell::from_styled(line.short.clone(), Some(short_width));
                    row.append(short_cell);
                    row.append_sep(" ");
                }
                if option_width > 0 {
                    let option_cell = RowCell::from_styled(line.name.clone(), Some(option_width));
                    row.append(option_cell);
                    row.append_sep(" ");
                }
                if tag_width > 0 {
                    let tag_cell = RowCell::from_styled(line.tags.clone(), Some(tag_width));
                    row.append(tag_cell);
                }
                row.append_one_line(": ");
                let description_cell = RowCell::from_styled(line.description.clone(), None);
                row.append(description_cell);
                result.append(&mut row.render());
                let detail_width = term_width
                    .map(|tw| tw.saturating_sub(intent * 2))
                    .filter(|w| *w > MIN_TERM_WIDTH);
                let wrapped_detail = line
                    .detail
                    .iter()
                    .flat_map(|detail| detail.soft_wrap(detail_width))
                    .map(|detail| StyledString::from(" ".repeat(intent * 2)) + detail)
                    .collect::<Vec<_>>();
                result.extend(wrapped_detail);
            }
        }
        result
    }
}

pub trait CmdTemplate {
    fn render<'a>(
        &self,
        cmd: &CmdInterface,
        components: Option<Box<dyn Iterator<Item = &'a ComponentMetadata> + 'a>>,
        term_width: Option<usize>,
    ) -> Vec<StyledString>;
}

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
                let p_args = cmd.positional_args.iter().map(|arg| format!("<{}>", arg)).collect::<Vec<_>>()
                    .join(" ");
                sig_row.append_styled(
                    vec![StyledString::from_content(&p_args.bold())],
                    None,
                );
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
                .filter(|w| *w > MIN_TERM_WIDTH);
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
                .filter(|w| *w > MIN_TERM_WIDTH);
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

pub trait ArgTemplate {
    fn render<'a>(&self, arg: &CmdArg, term_width: Option<usize>) -> Vec<StyledString>;
}

pub struct HardCodeArgTemplate {
    pub intent: usize,
}

impl Default for HardCodeArgTemplate {
    fn default() -> Self {
        HardCodeArgTemplate { intent: 2 }
    }
}

fn handle_prop(prop: &CmdArg) -> HelpTableLine {
    let name = vec![StyledString::from_content(&prop.name.clone().bold())];
    let mut tags = vec![];
    if prop.required {
        tags.push(StyledString::from_content(&"[Required]".yellow()));
    }
    let mut description = vec![];
    if let Some(help) = &prop.help {
        let styled_help: StyledString = help.short.clone().into();
        description.append(&mut styled_help.split_lines());
    }
    if let Some(default) = &prop.default {
        let value = default.value.clone().blue().on_yellow();
        let styled_default = StyledString::from_vec(vec!["Default: ".to_owned().stylize(), value]);
        description.push(styled_default);
    }
    HelpTableLine {
        name,
        tags,
        description,
        ..Default::default()
    }
}

impl ArgTemplate for HardCodeArgTemplate {
    fn render<'a>(&self, arg: &CmdArg, term_width: Option<usize>) -> Vec<StyledString> {
        let mut result = vec![];

        result.push(StyledString::from("Argument"));

        let mut arg_name_row = Row::new();
        arg_name_row.append_sep(" ".repeat(self.intent));
        arg_name_row.append_styled(
            vec![StyledString::from_content(&arg.name.clone().bold())],
            None,
        );
        let arg_name_len = arg.name.len();
        if let Some(arg_type) = &arg.arg_type {
            arg_name_row.append_one_line(" : ");
            let arg_type = StyledString::from_content(&format!("{}", arg_type).italic());
            arg_name_row.append_styled(arg_type.split_lines(), None);
        }
        let type_len = arg
            .arg_type
            .as_ref()
            .map(|t| t.to_string().len() + 3)
            .unwrap_or(0);
        if let Some(short) = arg.help.as_ref().map(|help| &help.short) {
            arg_name_row.append_one_line(" - ");
            let mut description = vec![];
            let short_help = StyledString::from_content(&short.clone().italic());
            let width = term_width
                .map(|tw| tw.saturating_sub(self.intent + arg_name_len + type_len + 3))
                .filter(|w| *w > MIN_TERM_WIDTH);
            description.append(&mut short_help.split_lines());
            if let Some(default) = &arg.default {
                let value = default.value.clone().blue().on_yellow();
                let styled_default =
                    StyledString::from_vec(vec!["Default: ".to_owned().stylize(), value]);
                description.push(styled_default);
            }
            arg_name_row.append_styled(description, width);
        }
        result.append(&mut arg_name_row.render());

        if !arg.options.is_empty() {
            let mut options = arg
                .options
                .iter()
                .map(|s| {
                    if s.len() == 1 {
                        format!("-{}", s)
                    } else {
                        format!("--{}", s)
                    }
                })
                .collect::<Vec<_>>();
            options.sort_by_key(|o| o.len());
            let options_line = options.join(" ");
            let options_line = StyledString::from_content(&options_line.bold());
            let mut options_row = Row::new();
            options_row.append_sep(" ".repeat(self.intent));
            options_row.append_styled(
                vec![options_line],
                term_width
                    .map(|tw| tw.saturating_sub(self.intent))
                    .filter(|w| *w > MIN_TERM_WIDTH),
            );
            result.append(&mut options_row.render());
        }

        let mut tags = vec![];
        if arg.required {
            tags.push(vec!["[Required]".to_owned().yellow()]);
        }
        if !tags.is_empty() {
            let tags_content = tags.join(&" ".to_owned().stylize());
            let tags_line = StyledString::from_vec(tags_content);
            let mut tags_row = Row::new();
            tags_row.append_sep(" ".repeat(self.intent));
            tags_row.append_styled(
                vec![tags_line],
                term_width
                    .map(|tw| tw.saturating_sub(self.intent))
                    .filter(|w| *w > MIN_TERM_WIDTH),
            );
            result.append(&mut tags_row.render());
        }

        if let Some(long) = arg.help.as_ref().and_then(|h| h.long.as_ref()) {
            let mut long_help_row = Row::new();
            long_help_row.append_sep(" ".repeat(self.intent * 2));
            let long_help = StyledString::from(long);
            let long_help_lines = long_help.split_lines();
            let width = term_width
                .map(|tw| tw.saturating_sub(self.intent * 2))
                .filter(|w| *w > MIN_TERM_WIDTH);
            long_help_row.append_styled(long_help_lines, width);
            result.append(&mut long_help_row.render());
        }

        if let Some(arg_type) = &arg.arg_type {
            let mut enum_lines = vec![];
            match &arg_type {
                ArgType::Str(CmdStringArg { r#enum, format: _ })
                | ArgType::Number(CmdNumberArg { r#enum, format: _ }) => {
                    if let Some(enum_type) = r#enum {
                        for item in &enum_type.items {
                            enum_lines.push(HelpTableLine {
                                name: vec![StyledString::from_content(&item.name.clone().bold())],
                                description: vec![StyledString::from(&item.value)],
                                ..Default::default()
                            });
                        }
                    }
                }
                _ => {}
            }

            let mut property_lines = vec![];
            match &arg_type {
                ArgType::Str(str_arg) => match &str_arg.format {
                    Some(cmd_string_arg::Format::String(str)) => {
                        if let Some(pattern) = &str.pattern {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Pattern")],
                                description: vec![StyledString::from(pattern)],
                                ..Default::default()
                            });
                        }
                        if let Some(length) = &str.max_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Max Length")],
                                description: vec![StyledString::from(&length.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(length) = &str.min_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Min Length")],
                                description: vec![StyledString::from(&length.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_string_arg::Format::Duration(duration)) => {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Protocol")],
                            description: vec![StyledString::from(
                                duration.protocol().as_str_name(),
                            )],
                            ..Default::default()
                        });
                    }
                    Some(cmd_string_arg::Format::Date(date)) => {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Protocol")],
                            description: vec![StyledString::from(date.protocol().as_str_name())],
                            ..Default::default()
                        });
                    }
                    Some(cmd_string_arg::Format::DateTime(date_time)) => {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Protocol")],
                            description: vec![StyledString::from(
                                date_time.protocol().as_str_name(),
                            )],
                            ..Default::default()
                        });
                        if let Some(ms_length) = &date_time.ms_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("MS Length")],
                                description: vec![StyledString::from(&ms_length.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_string_arg::Format::Time(time)) => {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Protocol")],
                            description: vec![StyledString::from(time.protocol().as_str_name())],
                            ..Default::default()
                        });
                        if let Some(ms_length) = &time.ms_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("MS Length")],
                                description: vec![StyledString::from(&ms_length.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_string_arg::Format::Uuid(uuid)) => {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Case")],
                            description: vec![StyledString::from(uuid.case().as_str_name())],
                            ..Default::default()
                        });
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("No Hyphen")],
                            description: vec![StyledString::from(uuid.no_hyphen.to_string())],
                            ..Default::default()
                        });
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("With Braces")],
                            description: vec![StyledString::from(uuid.with_braces.to_string())],
                            ..Default::default()
                        });
                    }
                    Some(cmd_string_arg::Format::ResourceId(resource_id)) => {
                        if !resource_id.templates.is_empty() {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Templates")],
                                description: resource_id
                                    .templates
                                    .iter()
                                    .map(|t| StyledString::from(t))
                                    .collect(),
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_string_arg::Format::ResourceLocation(resource_location)) => {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("No Resource Group Default")],
                            description: vec![StyledString::from(
                                resource_location.no_rg_default.to_string(),
                            )],
                            ..Default::default()
                        });
                    }
                    _ => {}
                },
                ArgType::Number(number_arg) => match &number_arg.format {
                    Some(cmd_number_arg::Format::Int32(int32)) => {
                        if let Some(maximum) = &int32.maximum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum")],
                                description: vec![StyledString::from(&maximum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(minimum) = &int32.minimum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum")],
                                description: vec![StyledString::from(&minimum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(multiple_of) = &int32.multiple_of {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Multiple")],
                                description: vec![StyledString::from(&multiple_of.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_number_arg::Format::Int64(int64)) => {
                        if let Some(maximum) = &int64.maximum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum")],
                                description: vec![StyledString::from(&maximum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(minimum) = &int64.minimum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum")],
                                description: vec![StyledString::from(&minimum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(multiple_of) = &int64.multiple_of {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Multiple")],
                                description: vec![StyledString::from(&multiple_of.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_number_arg::Format::Uint32(uint32)) => {
                        if let Some(maximum) = &uint32.maximum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum")],
                                description: vec![StyledString::from(&maximum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(minimum) = &uint32.minimum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum")],
                                description: vec![StyledString::from(&minimum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(multiple_of) = &uint32.multiple_of {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Multiple")],
                                description: vec![StyledString::from(&multiple_of.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_number_arg::Format::Uint64(uint64)) => {
                        if let Some(maximum) = &uint64.maximum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum")],
                                description: vec![StyledString::from(&maximum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(minimum) = &uint64.minimum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum")],
                                description: vec![StyledString::from(&minimum.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(multiple_of) = &uint64.multiple_of {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Multiple")],
                                description: vec![StyledString::from(&multiple_of.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_number_arg::Format::Float(float)) => {
                        if let Some(maximum) = &float.maximum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum")],
                                description: vec![StyledString::from(&maximum.to_string())],
                                ..Default::default()
                            });
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum Exclusive")],
                                description: vec![StyledString::from(
                                    &float.exclusive_maximum.to_string(),
                                )],
                                ..Default::default()
                            });
                        }
                        if let Some(minimum) = &float.minimum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum")],
                                description: vec![StyledString::from(&minimum.to_string())],
                                ..Default::default()
                            });
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum Exclusive")],
                                description: vec![StyledString::from(
                                    &float.exclusive_minimum.to_string(),
                                )],
                                ..Default::default()
                            });
                        }
                        if let Some(multiple_of) = &float.multiple_of {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Multiple")],
                                description: vec![StyledString::from(&multiple_of.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    Some(cmd_number_arg::Format::Double(double)) => {
                        if let Some(maximum) = &double.maximum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum")],
                                description: vec![StyledString::from(&maximum.to_string())],
                                ..Default::default()
                            });
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Maximum Exclusive")],
                                description: vec![StyledString::from(
                                    &double.exclusive_maximum.to_string(),
                                )],
                                ..Default::default()
                            });
                        }
                        if let Some(minimum) = &double.minimum {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum")],
                                description: vec![StyledString::from(&minimum.to_string())],
                                ..Default::default()
                            });
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Minimum Exclusive")],
                                description: vec![StyledString::from(
                                    &double.exclusive_minimum.to_string(),
                                )],
                                ..Default::default()
                            });
                        }
                        if let Some(multiple_of) = &double.multiple_of {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Multiple")],
                                description: vec![StyledString::from(&multiple_of.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                    _ => {}
                },
                ArgType::Boolean(bool_arg) => {
                    if !bool_arg.reverse_options.is_empty() {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Reverse Options")],
                            description: bool_arg
                                .reverse_options
                                .iter()
                                .map(|s| StyledString::from(s))
                                .collect(),
                            ..Default::default()
                        });
                    }
                }
                ArgType::Object(object_arg) => {
                    if let Some(cls_type) = &object_arg.cls_type {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Class Type")],
                            description: vec![StyledString::from(cls_type)],
                            ..Default::default()
                        });
                    }
                    if let Some(format) = &object_arg.format {
                        if let Some(min_length) = &format.min_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Min Length")],
                                description: vec![StyledString::from(&min_length.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(max_length) = &format.max_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Max Length")],
                                description: vec![StyledString::from(&max_length.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                }
                ArgType::Array(array_arg) => {
                    if let Some(cls_type) = &array_arg.cls_type {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Class Type")],
                            description: vec![StyledString::from(cls_type)],
                            ..Default::default()
                        });
                    }
                    if let Some(format) = &array_arg.format {
                        property_lines.push(HelpTableLine {
                            name: vec![StyledString::from("Unique")],
                            description: vec![StyledString::from(format.unique.to_string())],
                            ..Default::default()
                        });
                        if let Some(min_length) = &format.min_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Min Length")],
                                description: vec![StyledString::from(&min_length.to_string())],
                                ..Default::default()
                            });
                        }
                        if let Some(max_length) = &format.max_length {
                            property_lines.push(HelpTableLine {
                                name: vec![StyledString::from("Max Length")],
                                description: vec![StyledString::from(&max_length.to_string())],
                                ..Default::default()
                            });
                        }
                    }
                }
                _ => {}
            };

            if !(enum_lines.is_empty() && property_lines.is_empty()) {
                result.push(StyledString::from(""));
                result.push(StyledString::from(format!("{}", arg_type)));
            }
            if !enum_lines.is_empty() {
                result.push(StyledString::from(format!(
                    "{}Enum",
                    " ".repeat(self.intent)
                )));
                let mut enum_table = HelpTableLine::render_table(
                    &enum_lines,
                    term_width,
                    self.intent * 2,
                    self.intent,
                );
                result.append(&mut enum_table);
            }
            if !property_lines.is_empty() {
                result.append(&mut HelpTableLine::render_table(
                    &property_lines,
                    term_width,
                    self.intent,
                    self.intent,
                ));
            }

            let mut props_groups = HashMap::new();
            match arg_type {
                ArgType::Object(object_arg) => {
                    for prop in &object_arg.props {
                        let line = handle_prop(prop);
                        let group = props_groups
                            .entry(prop.group.clone())
                            .or_insert_with(|| vec![]);
                        group.push(line);
                    }
                    if let Some(additional_props) = &object_arg.additional_props {
                        if let Some(prop) = &additional_props.item {
                            let line = handle_prop(prop);
                            let group = props_groups
                                .entry("Additional Properties".to_string())
                                .or_insert_with(|| vec![]);
                            group.push(line);
                        }
                    }
                }
                _ => {}
            }

            if !props_groups.is_empty() {
                result.push(StyledString::from(""));
                result.append(&mut HelpTableLine::render_groups(
                    &props_groups,
                    term_width,
                    self.intent,
                ));
            }
        }

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
