use std::collections::HashMap;

use crate::output::{Row, RowCell, StyledString};

use super::MIN_DETAIL_WIDTH;

pub struct HelpTableLine {
    pub short: Vec<StyledString>,
    pub name: Vec<StyledString>,
    pub tags: Vec<StyledString>,
    pub description: Vec<StyledString>,
    pub detail: Vec<StyledString>,
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

    pub fn render_table(
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
                .filter(|w| *w > MIN_DETAIL_WIDTH);
            let detail_cell = RowCell::from_styled(line.detail.clone(), detail_width);
            detail_row.append(detail_cell);
            result.append(&mut detail_row.render());
        }
        result
    }

    pub fn render_groups(
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
                    .filter(|w| *w > MIN_DETAIL_WIDTH);
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