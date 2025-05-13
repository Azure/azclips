use std::collections::HashMap;

use az_core::pb::{
    cmd_arg, cmd_number_arg, cmd_string_arg, CmdArg, CmdArgEnum, CmdArrayArg, CmdArrayFormat,
    CmdBooleanArg, CmdDateFormat, CmdDateTimeFormat, CmdDoubleFormat, CmdDurationFormat,
    CmdFloatFormat, CmdInt32Format, CmdInt64Format, CmdInterface, CmdNumberArg, CmdObjectArg,
    CmdObjectFormat, CmdResourceIdFormat, CmdResourceLocationFormat, CmdStringArg, CmdStringFormat,
    CmdTimeFormat, CmdUint32Format, CmdUint64Format, CmdUuidFormat, ComponentMetadata,
};

use crate::output::{style::Stylize, Row, RowCell, StyledString};

use super::MIN_DETAIL_WIDTH;

#[derive(Debug, Clone, Default)]
pub struct HelpTableLine {
    pub short: Vec<StyledString>,
    pub name: Vec<StyledString>,
    pub tags: Vec<StyledString>,
    pub description: Vec<StyledString>,
    pub detail: Vec<StyledString>,
}

macro_rules! col_width {
    ($name:ident, $col:ident) => {
        fn $name<'a>(table: impl IntoIterator<Item = &'a Self>) -> usize {
            table
                .into_iter()
                .map(|line| Self::calc_width(&line.$col))
                .max()
                .unwrap_or(0)
        }
    };
}

impl HelpTableLine {
    pub fn new_kv(key: impl Into<String>, value: impl Into<String>) -> Self {
        Self {
            short: vec![],
            name: vec![StyledString::from(key.into())],
            tags: vec![],
            description: vec![],
            detail: vec![StyledString::from(value.into())],
        }
    }

    fn calc_width(cell: &Vec<StyledString>) -> usize {
        cell.iter().map(|s| s.term_len()).max().unwrap_or(0)
    }

    col_width!(short_width, short);
    col_width!(name_width, name);
    col_width!(tags_width, tags);

    pub fn render_table(
        table: Vec<Self>,
        term_width: Option<usize>,
        offset: usize,
        intent: usize,
    ) -> Vec<StyledString> {
        let short_width = Self::short_width(&table);
        let option_width = Self::name_width(&table);
        let tag_width = Self::tags_width(&table);
        let mut result = vec![];
        for line in table {
            let mut row = Row::new();
            row.append_sep(" ".repeat(offset));
            if short_width > 0 {
                row.append_styled(line.short, Some(short_width));
                row.append_sep(" ");
            }
            if option_width > 0 {
                row.append_styled(line.name, Some(option_width));
                row.append_sep(" ");
            }
            if tag_width > 0 {
                row.append_styled(line.tags, Some(tag_width));
            }
            if !line.description.is_empty() {
                row.append_one_line(": ");
                row.append_styled(line.description, None);
            }
            result.append(&mut row.render());
            let mut detail_row = Row::new();
            detail_row.append_sep(" ".repeat(offset + intent));
            let detail_width = term_width
                .map(|tw| tw.saturating_sub(offset + intent * 2))
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            detail_row.append_styled(line.detail.clone(), detail_width);
            result.append(&mut detail_row.render());
        }
        result
    }

    pub fn render_groups(
        groups: HashMap<String, Vec<Self>>,
        term_width: Option<usize>,
        intent: usize,
    ) -> Vec<StyledString> {
        let short_width = Self::short_width(groups.values().flat_map(|group| group.iter()));
        let option_width = Self::name_width(groups.values().flat_map(|group| group.iter()));
        let tag_width = Self::tags_width(groups.values().flat_map(|group| group.iter()));
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
                    row.append_styled(line.short, Some(short_width));
                    row.append_sep(" ");
                }
                if option_width > 0 {
                    row.append_styled(line.name, Some(option_width));
                    row.append_sep(" ");
                }
                if tag_width > 0 {
                    row.append_styled(line.tags, Some(tag_width));
                }
                row.append_one_line(": ");
                let description_cell = RowCell::from_styled(line.description, None);
                row.append(description_cell);
                result.append(&mut row.render());
                let detail_width = term_width
                    .map(|tw| tw.saturating_sub(intent * 2))
                    .filter(|w| *w > MIN_DETAIL_WIDTH);
                let wrapped_detail = line
                    .detail
                    .into_iter()
                    .flat_map(|detail| detail.soft_wrap(detail_width))
                    .map(|detail| StyledString::from(" ".repeat(intent * 2)) + detail)
                    .collect::<Vec<_>>();
                result.extend(wrapped_detail);
            }
        }
        result
    }
}

pub trait HelpLineDisplay {
    type Config<'a>;

    fn help_line<'a>(&self, config: Self::Config<'a>) -> HelpTableLine;
}

impl HelpLineDisplay for ComponentMetadata {
    type Config<'a> = ();

    fn help_line<'a>(&self, _: Self::Config<'a>) -> HelpTableLine {
        HelpTableLine {
            short: vec![],
            name: vec![self.name.clone().bold().into()],
            tags: vec![],
            description: self
                .help
                .as_ref()
                .map(|help| &help.short)
                .map(|desc| StyledString::from(desc.clone().italic()).split_lines())
                .unwrap_or(vec![]),
            detail: vec![],
        }
    }
}

impl HelpLineDisplay for CmdInterface {
    type Config<'a> = ();

    fn help_line<'a>(&self, _: Self::Config<'a>) -> HelpTableLine {
        HelpTableLine {
            name: vec![self.name.clone().bold().into()],
            description: self
                .help
                .as_ref()
                .map(|help| &help.short)
                .map(|desc| StyledString::from(desc.clone().italic()).split_lines())
                .unwrap_or(vec![]),
            ..Default::default()
        }
    }
}

pub struct CmdArgDisplayConfig<'a> {
    pub show_options: bool,
    pub positions: Option<&'a [String]>,
}

impl HelpLineDisplay for CmdArg {
    type Config<'a> = CmdArgDisplayConfig<'a>;

    fn help_line<'a>(&self, config: CmdArgDisplayConfig<'a>) -> HelpTableLine {
        let short = if config.show_options {
            self.options
                .iter()
                .filter(|o| o.len() == 1)
                .map(|s| format!("-{}", s).bold())
                .map(|s| s.into())
                .collect::<Vec<_>>()
        } else {
            vec![]
        };
        let is_positional = config
            .positions
            .map(|positions| positions.contains(&self.name))
            .unwrap_or(false);
        let name = if config.show_options && !self.options.is_empty() {
            self.options
                .iter()
                .filter(|o| o.len() != 1)
                .map(|s| format!("--{}", s).bold())
                .map(|s| s.into())
                .collect::<Vec<_>>()
        } else if config.show_options && is_positional {
            vec![format!("<{}>", self.name.to_uppercase()).bold().into()]
        } else {
            vec![self.name.clone().bold().into()]
        };
        let mut tags = vec![];
        if self.required {
            tags.push("[Required]".yellow().into());
        }
        let mut description = vec![];
        if let Some(help) = &self.help {
            let styled_help: StyledString = help.short.clone().into();
            description.append(&mut styled_help.split_lines());
        }
        if let Some(default) = &self.default {
            let value = default.value.clone().blue().on_yellow();
            let styled_default = StyledString::new() + "Default: ".stylize() + value;
            description.push(styled_default);
        }
        HelpTableLine {
            short,
            name,
            tags,
            description,
            ..Default::default()
        }
    }
}

pub trait HelpTableDisplay {
    fn help_table(&self) -> Vec<HelpTableLine>;
}

fn append_table(table: &mut Vec<HelpTableLine>, key: impl ToString, value: &Option<impl ToString>) {
    if let Some(value) = value {
        table.push(HelpTableLine::new_kv(key.to_string(), value.to_string()));
    }
}

pub fn arg_format_table(arg_type: &cmd_arg::ArgType) -> Vec<HelpTableLine> {
    match arg_type {
        cmd_arg::ArgType::Str(arg) => arg.help_table(),
        cmd_arg::ArgType::Number(arg) => arg.help_table(),
        cmd_arg::ArgType::Boolean(arg) => arg.help_table(),
        cmd_arg::ArgType::Object(arg) => arg.help_table(),
        cmd_arg::ArgType::Array(arg) => arg.help_table(),
        cmd_arg::ArgType::Cls(_) => vec![],
        cmd_arg::ArgType::AnyType(_) => vec![],
    }
}

pub fn arg_enum_table(arg_type: &cmd_arg::ArgType) -> Vec<HelpTableLine> {
    match arg_type {
        cmd_arg::ArgType::Str(CmdStringArg {
            r#enum: Some(enum_type),
            format: _,
        })
        | cmd_arg::ArgType::Number(CmdNumberArg {
            r#enum: Some(enum_type),
            format: _,
        }) => enum_type.help_table(),
        cmd_arg::ArgType::Str(CmdStringArg {
            r#enum: None,
            format: _,
        }) => vec![],
        cmd_arg::ArgType::Number(CmdNumberArg {
            r#enum: None,
            format: _,
        }) => vec![],
        cmd_arg::ArgType::Boolean(_) => vec![],
        cmd_arg::ArgType::Object(_) => vec![],
        cmd_arg::ArgType::Array(_) => vec![],
        cmd_arg::ArgType::Cls(_) => vec![],
        cmd_arg::ArgType::AnyType(_) => vec![],
    }
}

pub fn arg_props_tables(arg_type: &cmd_arg::ArgType) -> HashMap<String, Vec<HelpTableLine>> {
    let mut props_groups = HashMap::new();
    match arg_type {
        cmd_arg::ArgType::Object(object_arg) => {
            for prop in &object_arg.props {
                let line = prop.help_line(CmdArgDisplayConfig {
                    show_options: false,
                    positions: None,
                });
                let group = props_groups
                    .entry(prop.group.clone())
                    .or_insert_with(|| vec![]);
                group.push(line);
            }
            if let Some(additional_props) = &object_arg.additional_props {
                if let Some(prop) = &additional_props.item {
                    let line = prop.help_line(CmdArgDisplayConfig {
                        show_options: false,
                        positions: None,
                    });
                    let group = props_groups
                        .entry("Additional Properties".to_string())
                        .or_insert_with(|| vec![]);
                    group.push(line);
                }
            }
        }
        cmd_arg::ArgType::Array(_) => {}
        cmd_arg::ArgType::Str(_) => {}
        cmd_arg::ArgType::Number(_) => {}
        cmd_arg::ArgType::Boolean(_) => {}
        cmd_arg::ArgType::Cls(_) => {}
        cmd_arg::ArgType::AnyType(_) => {}
    }
    props_groups
}

impl HelpTableDisplay for CmdArgEnum {
    fn help_table(&self) -> Vec<HelpTableLine> {
        self.items
            .iter()
            .map(|item| HelpTableLine {
                name: vec![item.name.clone().bold().into()],
                description: vec![StyledString::from(&item.value)],
                ..Default::default()
            })
            .collect()
    }
}

impl HelpTableDisplay for CmdStringArg {
    fn help_table(&self) -> Vec<HelpTableLine> {
        match &self.format {
            Some(cmd_string_arg::Format::Binary(_)) => vec![],
            Some(cmd_string_arg::Format::Byte(_)) => vec![],
            Some(cmd_string_arg::Format::String(format)) => format.help_table(),
            Some(cmd_string_arg::Format::DateTime(format)) => format.help_table(),
            Some(cmd_string_arg::Format::Date(format)) => format.help_table(),
            Some(cmd_string_arg::Format::Time(format)) => format.help_table(),
            Some(cmd_string_arg::Format::Duration(format)) => format.help_table(),
            Some(cmd_string_arg::Format::Uuid(format)) => format.help_table(),
            Some(cmd_string_arg::Format::ResourceId(format)) => format.help_table(),
            Some(cmd_string_arg::Format::ResourceLocation(format)) => format.help_table(),
            Some(cmd_string_arg::Format::SubscriptionId(_)) => vec![],
            Some(cmd_string_arg::Format::ResourceGroupName(_)) => vec![],
            Some(cmd_string_arg::Format::Password(_)) => vec![],
            None => vec![],
        }
    }
}

impl HelpTableDisplay for CmdNumberArg {
    fn help_table(&self) -> Vec<HelpTableLine> {
        match &self.format {
            Some(cmd_number_arg::Format::Int32(format)) => format.help_table(),
            Some(cmd_number_arg::Format::Int64(format)) => format.help_table(),
            Some(cmd_number_arg::Format::Uint32(format)) => format.help_table(),
            Some(cmd_number_arg::Format::Uint64(format)) => format.help_table(),
            Some(cmd_number_arg::Format::Float(format)) => format.help_table(),
            Some(cmd_number_arg::Format::Double(format)) => format.help_table(),
            None => vec![],
        }
    }
}

impl HelpTableDisplay for CmdBooleanArg {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        if !self.reverse_options.is_empty() {
            table.push(HelpTableLine {
                name: vec![StyledString::from("Reverse Options")],
                description: self
                    .reverse_options
                    .iter()
                    .map(|s| StyledString::from(s))
                    .collect(),
                ..Default::default()
            });
        }
        table
    }
}

impl HelpTableDisplay for CmdObjectArg {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        append_table(&mut table, "Class Type", &self.cls_type);
        if let Some(format) = &self.format {
            table.append(&mut format.help_table());
        }
        table
    }
}

impl HelpTableDisplay for CmdArrayArg {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        append_table(&mut table, "Class Type", &self.cls_type);
        if let Some(format) = &self.format {
            table.append(&mut format.help_table());
        }
        table
    }
}

impl HelpTableDisplay for CmdStringFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        append_table(&mut table, "Pattern", &self.pattern);
        append_table(&mut table, "Max Length", &self.max_length);
        append_table(&mut table, "Min Length", &self.min_length);
        table
    }
}

impl HelpTableDisplay for CmdDurationFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv(
            "Protocol",
            self.protocol().as_str_name(),
        ));
        table
    }
}

impl HelpTableDisplay for CmdDateFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv(
            "Protocol",
            self.protocol().as_str_name(),
        ));
        table
    }
}

impl HelpTableDisplay for CmdDateTimeFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv(
            "Protocol",
            self.protocol().as_str_name(),
        ));
        append_table(&mut table, "MS Length", &self.ms_length);
        table
    }
}

impl HelpTableDisplay for CmdTimeFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv(
            "Protocol",
            self.protocol().as_str_name(),
        ));
        append_table(&mut table, "MS Length", &self.ms_length);
        table
    }
}

impl HelpTableDisplay for CmdUuidFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv("Case", self.case().as_str_name()));
        table.push(HelpTableLine::new_kv(
            "No Hyphen",
            self.no_hyphen.to_string(),
        ));
        table.push(HelpTableLine::new_kv(
            "With Braces",
            self.with_braces.to_string(),
        ));
        table
    }
}

impl HelpTableDisplay for CmdResourceIdFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        if !self.templates.is_empty() {
            table.push(HelpTableLine {
                name: vec![StyledString::from("Templates")],
                description: self
                    .templates
                    .iter()
                    .map(|t| StyledString::from(t))
                    .collect(),
                ..Default::default()
            });
        }
        table
    }
}

impl HelpTableDisplay for CmdResourceLocationFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv(
            "No Resource Group Default",
            self.no_rg_default.to_string(),
        ));
        table
    }
}

macro_rules! impl_help_table_integer {
    ($name:ident) => {
        impl HelpTableDisplay for $name {
            fn help_table(&self) -> Vec<HelpTableLine> {
                let mut table = vec![];
                append_table(&mut table, "Maximun", &self.maximum);
                append_table(&mut table, "Minimun", &self.minimum);
                append_table(&mut table, "Multiple", &self.multiple_of);
                table
            }
        }
    };
}

impl_help_table_integer!(CmdInt32Format);
impl_help_table_integer!(CmdInt64Format);
impl_help_table_integer!(CmdUint32Format);
impl_help_table_integer!(CmdUint64Format);

macro_rules! impl_help_table_float {
    ($name:ident) => {
        impl HelpTableDisplay for $name {
            fn help_table(&self) -> Vec<HelpTableLine> {
                let mut table = vec![];
                if let Some(maximum) = &self.maximum {
                    table.push(HelpTableLine::new_kv("Maximum", maximum.to_string()));
                    table.push(HelpTableLine::new_kv(
                        "Maximum Exclusive",
                        self.exclusive_maximum.to_string(),
                    ));
                }
                if let Some(minimum) = &self.minimum {
                    table.push(HelpTableLine::new_kv("Minimum", minimum.to_string()));
                    table.push(HelpTableLine::new_kv(
                        "Minimum Exclusive",
                        self.exclusive_minimum.to_string(),
                    ));
                }
                append_table(&mut table, "Multiple", &self.multiple_of);
                table
            }
        }
    };
}

impl_help_table_float!(CmdFloatFormat);
impl_help_table_float!(CmdDoubleFormat);

impl HelpTableDisplay for CmdObjectFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        append_table(&mut table, "Max Length", &self.max_length);
        append_table(&mut table, "Min Length", &self.min_length);
        table
    }
}

impl HelpTableDisplay for CmdArrayFormat {
    fn help_table(&self) -> Vec<HelpTableLine> {
        let mut table = vec![];
        table.push(HelpTableLine::new_kv("Unique", self.unique.to_string()));
        append_table(&mut table, "Max Length", &self.max_length);
        append_table(&mut table, "Min Length", &self.min_length);
        table
    }
}
