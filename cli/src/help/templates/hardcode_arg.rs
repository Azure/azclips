use std::collections::HashMap;

use az_core::pb::command::{cmd_arg::ArgType, cmd_number_arg, cmd_string_arg, CmdArg, CmdNumberArg, CmdStringArg};
use crossterm::style::Stylize;

use crate::{help::{table::HelpTableLine, MIN_DETAIL_WIDTH}, output::{Row, StyledString}};

use super::ArgTemplate;


/// A hardcoded template for rendering argument.
/// 
/// ## Example
/// ```text
/// # Usage
/// Argument
///     Arg Name - [Type] : [Short Help]
///                         Default: [default]
///     [Options] [Options]
///     [Tag] [Tag]
///         [Long Help]
///
/// [Type] - [Format]
///     Enum
///         [Enum Key] : [Enum Value]
///     Protocol : XXX
///
/// [Group]
///     [Name] [Tag] : [Description]
///         [Long Help]
/// ```
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
                .filter(|w| *w > MIN_DETAIL_WIDTH);
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
                    .filter(|w| *w > MIN_DETAIL_WIDTH),
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
                    .filter(|w| *w > MIN_DETAIL_WIDTH),
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
                .filter(|w| *w > MIN_DETAIL_WIDTH);
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

