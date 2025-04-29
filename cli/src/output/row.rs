use std::fmt::Display;

use unicode_width::UnicodeWidthStr;

use super::StyledString;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Alignment {
    LEFT,
    CENTER,
    RIGHT,
}

#[derive(Debug, Clone)]
pub enum RowCell {
    OneLine(String),
    Layout {
        align: Alignment,
        width: Option<usize>,
        content: Vec<StyledString>,
    },
    Sep(String),
}

impl RowCell {
    pub fn from_string<S>(content: S) -> Self
    where
        S: ToString,
    {
        RowCell::OneLine(content.to_string())
    }

    pub fn from_styled(content: Vec<StyledString>, width: Option<usize>) -> Self {
        RowCell::Layout {
            align: Alignment::LEFT,
            width,
            content,
        }
    }

    pub fn from_sep<S>(content: S) -> Self
    where
        S: ToString,
    {
        RowCell::Sep(content.to_string())
    }
}

#[derive(Debug, Clone)]
pub struct Row(Vec<RowCell>);

impl Row {
    pub fn new() -> Self {
        Row(vec![])
    }

    pub fn append(&mut self, mut cell: RowCell) {
        if let RowCell::Layout {
            align,
            width,
            content,
        } = cell
        {
            let content = content
                .into_iter()
                .flat_map(|line| line.soft_wrap(width))
                .collect();
            cell = RowCell::Layout {
                align,
                width,
                content,
            };
        }
        self.0.push(cell);
    }

    pub fn append_styled(&mut self, content: Vec<StyledString>, width: Option<usize>) {
        self.0.push(RowCell::from_styled(content, width));
    }

    pub fn append_one_line<S>(&mut self, content: S)
    where
        S: ToString,
    {
        self.0.push(RowCell::from_string(content));
    }

    pub fn append_sep<S>(&mut self, content: S)
    where
        S: ToString,
    {
        self.0.push(RowCell::from_sep(content));
    }

    pub fn line_num(&self) -> usize {
        self.0
            .iter()
            .filter_map(|cell| match cell {
                RowCell::Layout {
                    align: _align,
                    width: _width,
                    content,
                } => Some(content.len()),
                _ => None,
            })
            .max()
            .unwrap_or(1)
    }

    pub fn term_len(&self) -> usize {
        self.0
            .iter()
            .map(|cell| match cell {
                RowCell::Layout {
                    align: _align,
                    width: _width,
                    content,
                } => content.iter().map(|s| s.term_len()).sum(),
                RowCell::OneLine(content) => content.width_cjk(),
                RowCell::Sep(content) => content.width_cjk(),
            })
            .sum()
    }

    pub fn render(self) -> Vec<StyledString> {
        let line_num = self.line_num();
        let mut result = vec![StyledString::new(); line_num];
        for cell in self.0 {
            match cell {
                RowCell::Layout {
                    align,
                    width,
                    content,
                } => {
                    let content_height = content.len();
                    content
                        .into_iter()
                        .enumerate()
                        .for_each(|(idx, cell_line)| {
                            let line = &mut result[idx];
                            if let Some(width) = width {
                                let padding = width.saturating_sub(cell_line.term_len());
                                match align {
                                    Alignment::CENTER => {
                                        let left_padding = padding / 2;
                                        let right_padding = padding - left_padding;
                                        *line += " ".repeat(left_padding);
                                        *line += cell_line;
                                        *line += " ".repeat(right_padding);
                                    }
                                    Alignment::LEFT => {
                                        *line += cell_line;
                                        *line += " ".repeat(padding);
                                    }
                                    Alignment::RIGHT => {
                                        *line += " ".repeat(padding);
                                        *line += cell_line;
                                    }
                                }
                            } else {
                                *line += cell_line;
                            }
                        });
                    if let Some(width) = width {
                        for i in content_height..line_num {
                            let line = &mut result[i];
                            *line += " ".repeat(width);
                        }
                    }
                }
                RowCell::OneLine(content) => {
                    for i in 0..line_num {
                        let line = &mut result[i];
                        if i == 0 {
                            *line += content.clone();
                        } else {
                            *line += " ".repeat(content.width_cjk());
                        }
                    }
                }
                RowCell::Sep(content) => {
                    for i in 0..line_num {
                        let line = &mut result[i];
                        *line += content.clone();
                    }
                }
            }
        }
        result
    }
}

impl Display for Row {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let line_num = self.line_num();
        for num in 0..line_num {
            for cell in &self.0 {
                match cell {
                    RowCell::Layout {
                        align,
                        width,
                        content,
                    } => {
                        let cell_line = content.get(num);
                        if let Some(width) = width.as_ref() {
                            if let Some(cell_line) = cell_line {
                                match align {
                                    Alignment::CENTER => {
                                        write!(formatter, "{:^width$}", cell_line, width = width)?;
                                    }
                                    Alignment::LEFT => {
                                        write!(formatter, "{:<width$}", cell_line, width = width)?;
                                    }
                                    Alignment::RIGHT => {
                                        write!(formatter, "{:>width$}", cell_line, width = width)?;
                                    }
                                }
                            } else {
                                write!(formatter, "{}", " ".repeat(*width))?;
                            }
                        } else {
                            if let Some(cell_line) = cell_line {
                                write!(formatter, "{}", cell_line)?;
                            }
                        }
                    }
                    RowCell::OneLine(content) => {
                        if num == 0 {
                            write!(formatter, "{}", content)?;
                        } else {
                            write!(formatter, "{}", " ".repeat(content.width_cjk()))?;
                        }
                    }
                    RowCell::Sep(content) => {
                        write!(formatter, "{}", content)?;
                    }
                }
            }
            writeln!(formatter)?;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_line_display() {
        let cell1 = RowCell::Layout {
            align: Alignment::LEFT,
            width: Some(10),
            content: vec!["Cell1".to_string().into()],
        };
        let cell2 = RowCell::Layout {
            align: Alignment::CENTER,
            width: Some(10),
            content: vec!["Cell2".to_string().into()],
        };
        let cell3 = RowCell::Layout {
            align: Alignment::RIGHT,
            width: Some(10),
            content: vec!["Cell3".to_string().into()],
        };

        let line = Row(vec![
            RowCell::from_string("|"),
            cell1,
            RowCell::from_string("|"),
            cell2,
            RowCell::from_string("|"),
            cell3,
            RowCell::from_string("|"),
        ]);

        let expected_output = "|Cell1     |  Cell2   |     Cell3|\n";
        assert_eq!(line.to_string(), expected_output);

        let lines = line.render();
        assert_eq!(lines.len(), 1);
        assert_eq!(lines[0].to_string(), "|Cell1     |  Cell2   |     Cell3|");
    }

    #[test]
    fn test_line_display_with_wrapping() {
        let cell1 = RowCell::Layout {
            align: Alignment::LEFT,
            width: Some(5),
            content: vec!["Hello\nWorld".into()],
        };
        let cell2 = RowCell::Layout {
            align: Alignment::CENTER,
            width: Some(5),
            content: vec!["Rust".into()],
        };
        let cell3 = RowCell::Layout {
            align: Alignment::RIGHT,
            width: Some(5),
            content: vec!["New Programming Lang".into()],
        };

        let mut line = Row::new();
        line.append_sep("|");
        line.append(cell1);
        line.append_sep("|");
        line.append(cell2);
        line.append_sep("|");
        line.append(cell3);
        line.append_sep("|");

        let expected_output =
            "|Hello|Rust |  New|\n|World|     |Programming|\n|     |     | Lang|\n";
        assert_eq!(line.to_string(), expected_output);

        let lines = line.render();
        assert_eq!(lines.len(), 3);
        assert_eq!(lines[0].to_string(), "|Hello|Rust |  New|");
    }
}
