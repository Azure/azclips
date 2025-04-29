use std::collections::HashMap;

use crate::output::{Row, RowCell, StyledString};

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
