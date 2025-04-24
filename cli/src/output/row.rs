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
    pub fn from_string<S>(content: S) -> Self where S: ToString {
        RowCell::OneLine(content.to_string())
    }

    pub fn from_styled(content: Vec<StyledString>, width: Option<usize>) -> Self {
        RowCell::Layout {
            align: Alignment::LEFT,
            width: width,
            content,
        }
    }

    pub fn from_sep<S>(content: S) -> Self where S: ToString {
        RowCell::Sep(content.to_string())
    }
}


#[derive(Debug, Clone)]
pub struct Row(pub Vec<RowCell>);

impl Row {
    pub fn new() -> Self {
        Row(vec![])
    }

    pub fn append(&mut self, cell: RowCell) {
        self.0.push(cell);
    }

    pub fn append_styled(&mut self, content: Vec<StyledString>, width: Option<usize>) {
        self.0.push(RowCell::from_styled(content, width));
    }

    pub fn append_one_line<S>(&mut self, content: S) where S: ToString {
        self.0.push(RowCell::from_string(content));
    }

    pub fn append_sep<S>(&mut self, content: S) where S: ToString {
        self.0.push(RowCell::from_sep(content));
    }

    pub fn soft_wrap(&self) -> Self {
        let cells: Vec<RowCell> = self.0.iter().map(|cell| {
            match cell {
                RowCell::Layout { align, width, content } => {
                    RowCell::Layout { align: *align, width: *width, content: content.iter().flat_map(|line| line.soft_wrap(*width)).collect() }
                },
                _ => {
                    cell.clone()
                }
            }
        }).collect();
        Row(cells)
    }

    pub fn line_num(&self) -> usize {
        self.0.iter().filter_map(|cell| {
            match cell {
                RowCell::Layout { align: _align, width: _width, content } => {
                    Some(content.len())
                },
                _ => None,
            }
        }).max().unwrap_or(1)
    }

    pub fn render(&self) -> Vec<StyledString> {
        let mut result = vec![];
        let wrapped = self.soft_wrap();
        let line_num = wrapped.line_num();
        for i in 0..line_num {
            let mut line = StyledString::new();
            for cell in &wrapped.0 {
                match cell {
                    RowCell::Layout { align, width, content } => {
                        if let Some(width) = width {
                            if let Some(cell_line) = content.get(i) {
                                let padding = width.saturating_sub(cell_line.term_len());
                                match align {
                                    Alignment::CENTER => {
                                        let left_padding = padding / 2;
                                        let right_padding = padding - left_padding;
                                        line = line + " ".repeat(left_padding).into() + cell_line.clone() + " ".repeat(right_padding).into();
                                    },
                                    Alignment::LEFT => {
                                        line = line + cell_line.clone() + " ".repeat(padding).into();
                                    },
                                    Alignment::RIGHT => {
                                        line = line + " ".repeat(padding).into() + cell_line.clone();
                                    },
                                }
                            } else {
                                line = line + " ".repeat(*width).into();
                            }
                        } else {
                            if let Some(cell_line) = content.get(i) {
                                line = line + cell_line.clone();
                            }
                        }
                    },
                    RowCell::OneLine(content) => {
                        if i == 0 {
                            line = line + content.clone().into();
                        } else {
                            line = line + " ".repeat(content.width_cjk()).into();
                        }
                    },
                    RowCell::Sep(content) => {
                        line = line + content.clone().into();
                    },
                }
            }
            result.push(line);
        }
        result
    }
}

impl Display for Row {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let wrapped = self.soft_wrap();
        let line_num = wrapped.line_num();
        for num in 0..line_num {
            for cell in &wrapped.0 {
                match cell {
                    RowCell::Layout { align, width, content } => {
                        let cell_line = content.get(num);
                        if let Some(width) = width.as_ref() {
                            if let Some(cell_line) = cell_line {
                                match align {
                                    Alignment::CENTER => {
                                        write!(formatter, "{:^width$}", cell_line, width = width)?;
                                    },
                                    Alignment::LEFT => {
                                        write!(formatter, "{:<width$}", cell_line, width = width)?;
                                    },
                                    Alignment::RIGHT => {
                                        write!(formatter, "{:>width$}", cell_line, width = width)?;
                                    },
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
                    },
                    RowCell::Sep(content) => {
                        write!(formatter, "{}", content)?;
                    },
                }
            }
            writeln!(formatter)?;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use crossterm::style::{ContentStyle, StyledContent};

    use super::*;
    use crate::output::StyledString;
    

    #[test]
    fn test_line_display() {
        let style = ContentStyle::default();
        let cell1 = RowCell::Layout {
            align: Alignment::LEFT,
            width: Some(10),
            content: vec![StyledString(vec![StyledContent::new(style.clone(), "Cell1".to_string())])],
        };
        let cell2 = RowCell::Layout {
            align: Alignment::CENTER,
            width: Some(10),
            content: vec![StyledString(vec![StyledContent::new(style.clone(), "Cell2".to_string())])],
        };
        let cell3 = RowCell::Layout {
            align: Alignment::RIGHT,
            width: Some(10),
            content: vec![StyledString(vec![StyledContent::new(style.clone(), "Cell3".to_string())])],
        };

        let line = Row(vec![RowCell::from_string("|"), cell1, RowCell::from_string("|"), cell2, RowCell::from_string("|"), cell3, RowCell::from_string("|")]);

        let expected_output = "|Cell1     |  Cell2   |     Cell3|\n";
        assert_eq!(line.to_string(), expected_output);

        let lines = line.render();
        assert_eq!(lines.len(), 1);
        assert_eq!(lines[0].to_string(), "|Cell1     |  Cell2   |     Cell3|");
    }

    #[test]
    fn test_line_display_with_wrapping() {
        let style = ContentStyle::default();
        let cell1 = RowCell::Layout {
            align: Alignment::LEFT,
            width: Some(5),
            content: vec![StyledString(vec![StyledContent::new(style.clone(), "Hello\nWorld".to_string())])],
        };
        let cell2 = RowCell::Layout {
            align: Alignment::CENTER,
            width: Some(5),
            content: vec![StyledString(vec![StyledContent::new(style.clone(), "Rust".to_string())])],
        };
        let cell3 = RowCell::Layout {
            align: Alignment::RIGHT,
            width: Some(5),
            content: vec![StyledString(vec![StyledContent::new(style.clone(), "New Programming Lang".to_string())])],
        };

        let line = Row(vec![RowCell::from_sep("|"), cell1, RowCell::from_sep("|"), cell2, RowCell::from_sep("|"), cell3, RowCell::from_sep("|")]);

        let expected_output = "|Hello|Rust |  New|\n|World|     |Programming|\n|     |     | Lang|\n";
        assert_eq!(line.to_string(), expected_output);

        let lines = line.render();
        assert_eq!(lines.len(), 3);
        assert_eq!(lines[0].to_string(), "|Hello|Rust |  New|");
    }
}