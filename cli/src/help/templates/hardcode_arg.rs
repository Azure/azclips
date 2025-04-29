use az_core::pb::command::CmdArg;

use crate::{
    help::{
        table::{arg_enum_table, arg_format_table, arg_props_tables, HelpTableLine},
        MIN_DETAIL_WIDTH,
    },
    output::{style::Stylize, Row, StyledString},
};

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

impl ArgTemplate for HardCodeArgTemplate {
    fn render<'a>(&self, arg: &CmdArg, term_width: Option<usize>) -> Vec<StyledString> {
        let mut result = vec![];

        result.push(StyledString::from("Argument"));

        let mut arg_name_row = Row::new();
        arg_name_row.append_sep(" ".repeat(self.intent));
        arg_name_row.append_styled(vec![arg.name.clone().bold().into()], None);
        if let Some(arg_type) = &arg.arg_type {
            arg_name_row.append_one_line(" : ");
            let arg_type = format!("{}", arg_type).italic().into();
            arg_name_row.append_styled(vec![arg_type], None);
        }
        if let Some(short) = arg.help.as_ref().map(|help| &help.short) {
            arg_name_row.append_one_line(" - ");
            let mut description = vec![];
            let short_help: StyledString = short.clone().italic().into();
            let width: Option<usize> = term_width
                .map(|tw| tw.saturating_sub(arg_name_row.term_len()))
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            description.append(&mut short_help.split_lines());
            if let Some(default) = &arg.default {
                let value = default.value.clone().blue().on_yellow();
                let styled_default = StyledString::new() + "Default: ".stylize() + value;
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
            let options_line = options.join(" ").bold().into();
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
            let enum_lines = arg_enum_table(arg_type);
            let format_lines = arg_format_table(arg_type);

            if !(enum_lines.is_empty() && format_lines.is_empty()) {
                result.push(StyledString::from(""));
                result.push(StyledString::from(format!("{}", arg_type)));
            }
            if !enum_lines.is_empty() {
                result.push(StyledString::from(format!(
                    "{}Enum",
                    " ".repeat(self.intent)
                )));
                let mut enum_table = HelpTableLine::render_table(
                    enum_lines,
                    term_width,
                    self.intent * 2,
                    self.intent,
                );
                result.append(&mut enum_table);
            }
            if !format_lines.is_empty() {
                result.append(&mut HelpTableLine::render_table(
                    format_lines,
                    term_width,
                    self.intent,
                    self.intent,
                ));
            }

            let props_groups = arg_props_tables(arg_type);

            if !props_groups.is_empty() {
                result.push(StyledString::from(""));
                result.append(&mut HelpTableLine::render_groups(
                    props_groups,
                    term_width,
                    self.intent,
                ));
            }
        }

        result
    }
}
