use std::{fmt::Display, ops::Add};

use crossterm::style::StyledContent;
use unicode_width::UnicodeWidthStr;

#[derive(Debug, Clone)]
pub struct StyledString(Vec<StyledContent<String>>);

impl StyledString {
    pub fn new() -> Self {
        StyledString(vec![])
    }

    pub fn from_content<S>(content: &StyledContent<S>) -> Self where S: Into<String> + Display {
        StyledString(vec![StyledContent::new(
            content.style().clone(),
            content.content().to_string(),
        )])
    }
    
    pub fn from_vec(vec: Vec<StyledContent<String>>) -> Self {
        StyledString(vec)
    }

    pub fn content(&self) -> String {
        self.0
            .iter()
            .map(|part| part.content().as_str())
            .collect::<Vec<_>>()
            .join("")
    }

    pub fn is_empty(&self) -> bool {
        self.0.is_empty()
    }

    pub fn append(&mut self, part: StyledContent<String>) {
        self.0.push(part);
    }

    pub fn split_lines(&self) -> Vec<Self> {
        let mut result = vec![];
        let mut cur_line = StyledString(vec![]);
        for part in &self.0 {
            let lines = part.content().lines().collect::<Vec<_>>();
            if lines.len() > 1 {
                cur_line.append(StyledContent::new(
                    part.style().clone(),
                    lines[0].to_string(),
                ));
                for line in &lines[1..] {
                    result.push(cur_line);
                    cur_line = StyledString(vec![StyledContent::new(
                        part.style().clone(),
                        line.to_string(),
                    )]);
                }
            } else {
                cur_line.append(part.clone());
            }
        }
        if !cur_line.is_empty() {
            result.push(cur_line);
        }
        return result;
    }

    pub fn term_len(&self) -> usize {
        self.0.iter().map(|part| part.content().width_cjk()).sum()
    }

    /// Soft Wrap the text in a limited width.
    ///
    /// **Note:** Should only be used in table display.
    fn soft_wrap_paragraph(&self, width: Option<usize>) -> Vec<Self> {
        if let Some(limit) = width {
            let mut result = vec![];
            let mut cur_line = StyledString(vec![]);
            let mut cur_width = 0;
            for part in &self.0 {
                // TODO: Should not replace the whitespace.
                // TODO: Should cut off words longger than the limit.
                for word in part.content().split_whitespace() {
                    let word_width = word.width_cjk();
                    if cur_width > 0 && cur_width + word_width + 1 > limit {
                        result.push(cur_line);
                        cur_line = StyledString(vec![]);
                        cur_width = 0;
                    }
                    if cur_width > 0 {
                        cur_line.append(StyledContent::new(
                            part.style().clone(),
                            " ".to_string(),
                        ));
                        cur_width += 1;
                    }
                    cur_line.append(StyledContent::new(
                        part.style().clone(),
                        word.to_string(),
                    ));
                    cur_width += word_width;
                }
            }
            if !cur_line.is_empty() {
                result.push(cur_line);
            }
            return result;
        } else {
            return vec![self.clone()];
        }
    }

    pub fn soft_wrap(&self, width: Option<usize>) -> Vec<Self> {
        self.split_lines()
            .into_iter()
            .flat_map(|line| line.soft_wrap_paragraph(width))
            .collect()
    }
}

impl<S> From<S> for StyledString
where
    S: Into<String>,
{
    fn from(value: S) -> Self {
        StyledString(vec![StyledContent::new(
            crossterm::style::ContentStyle::default(),
            value.into(),
        )])
    }
}

impl Add for StyledString {
    type Output = Self;

    fn add(self, rhs: Self) -> Self::Output {
        let mut result = self.clone();
        result.0.extend(rhs.0);
        result
    }
}

impl Display for StyledString {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let len = self.term_len();
        if len == 0 {
            return Ok(());
        }
        let raw = f.alternate();
        if raw {
            return self.content().fmt(f);
        }
        let fill = f.fill();
        let width = f.width().unwrap_or(0);
        let align = f.align();
        if len >= width || f.sign_aware_zero_pad() {
            for part in &self.0 {
                write!(f, "{}", part)?;
            }
            return Ok(());
        }
        match align {
            Some(std::fmt::Alignment::Right) => {
                write!(f, "{}", fill.to_string().repeat(width - len))?;
                for part in &self.0 {
                    write!(f, "{}", part)?;
                }
            },
            Some(std::fmt::Alignment::Center) => {
                let left = (width - len) / 2;
                let right = width - len - left;
                write!(f, "{}", fill.to_string().repeat(left))?;
                for part in &self.0 {
                    write!(f, "{}", part)?;
                }
                write!(f, "{}", fill.to_string().repeat(right))?;
            },
            _ => {
                for part in &self.0 {
                    write!(f, "{}", part)?;
                }
                write!(f, "{}", fill.to_string().repeat(width - len))?;
            }
        }
        Ok(())
    }
}

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

    pub fn from_vec(cells: Vec<RowCell>) -> Self {
        Row(cells)
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
    use super::*;
    use crossterm::style::ContentStyle;

    #[test]
    fn test_styled_string_display() {
        let style = ContentStyle::default();
        let styled_content1 = StyledContent::new(style.clone(), "Hello".to_string());
        let styled_content2 = StyledContent::new(style.clone(), " World".to_string());
        let styled_string = StyledString(vec![styled_content1, styled_content2]);

        assert_eq!(styled_string.to_string(), "Hello World");
    }

    #[test]
    fn test_styled_string_split_lines() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Hello\nWorld".to_string());
        let styled_string = StyledString(vec![styled_content]);

        let lines = styled_string.split_lines();
        assert_eq!(lines.len(), 2);
        assert_eq!(lines[0].to_string(), "Hello");
        assert_eq!(lines[1].to_string(), "World");
    }

    #[test]
    fn test_styled_string_term_len() {
        let style = ContentStyle::default();
        let styled_content1 = StyledContent::new(style.clone(), "Hello".to_string());
        let styled_content2 = StyledContent::new(style.clone(), " World".to_string());
        let styled_string = StyledString(vec![styled_content1, styled_content2]);

        assert_eq!(styled_string.term_len(), 11); // "Hello World" has 11 characters
    }

    #[test]
    fn test_styled_string_soft_wrap_paragraph() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Hello World!".to_string());
        let styled_string = StyledString(vec![styled_content]);

        let wrapped = styled_string.soft_wrap_paragraph(Some(5));
        assert_eq!(wrapped.len(), 2);
        assert_eq!(wrapped[0].to_string(), "Hello");
        assert_eq!(wrapped[1].to_string(), "World!");
    }

    #[test]
    fn test_styled_string_soft_wrap() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Hello\nWorld!".to_string());
        let styled_string = StyledString(vec![styled_content]);

        let wrapped = styled_string.soft_wrap(Some(5));
        assert_eq!(wrapped.len(), 2);
        assert_eq!(wrapped[0].to_string(), "Hello");
        assert_eq!(wrapped[1].to_string(), "World!");
    }

    #[test]
    fn test_styled_string_fmt_empty() {
        let styled_string = StyledString(vec![]);
        assert_eq!(styled_string.to_string(), "");
    }

    #[test]
    fn test_styled_string_fmt_alternate() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Test".to_string());
        let styled_string = StyledString(vec![styled_content]);
        
        // Using alternate format with # flag
        let formatted = format!("{:#}", styled_string);
        assert_eq!(formatted, "Test");
    }

    #[test]
    fn test_styled_string_fmt_width_longer() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Test".to_string());
        let styled_string = StyledString(vec![styled_content]);
        
        // Width is longer than content
        let formatted = format!("{:10}", styled_string);
        assert_eq!(formatted, "Test      "); // Right-padded with spaces (default left alignment)
    }

    #[test]
    fn test_styled_string_fmt_align_left() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Test".to_string());
        let styled_string = StyledString(vec![styled_content]);
        
        // Left alignment (default)
        let formatted = format!("{:<10}", styled_string);
        assert_eq!(formatted, "Test      ");
    }

    #[test]
    fn test_styled_string_fmt_align_right() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Test".to_string());
        let styled_string = StyledString(vec![styled_content]);
        
        // Right alignment
        let formatted = format!("{:>10}", styled_string);
        assert_eq!(formatted, "      Test");
    }

    #[test]
    fn test_styled_string_fmt_align_center() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Test".to_string());
        let styled_string = StyledString(vec![styled_content]);
        
        // Center alignment
        let formatted = format!("{:^10}", styled_string);
        assert_eq!(formatted, "   Test   ");
    }

    #[test]
    fn test_styled_string_fmt_custom_fill() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(style.clone(), "Test".to_string());
        let styled_string = StyledString(vec![styled_content]);
        
        // Custom fill character
        let formatted = format!("{:*^10}", styled_string);
        assert_eq!(formatted, "***Test***");
    }

    #[test]
    fn test_styled_string_complex_wrap() {
        let style = ContentStyle::default();
        let styled_content = StyledContent::new(
            style.clone(), 
            "This is a longer text that should wrap".to_string()
        );
        let styled_string = StyledString(vec![styled_content]);

        let wrapped = styled_string.soft_wrap_paragraph(Some(10));
        assert_eq!(wrapped.len(), 5);
        assert_eq!(wrapped[0].to_string(), "This is a");
        assert_eq!(wrapped[1].to_string(), "longer");
        assert_eq!(wrapped[2].to_string(), "text that");
        assert_eq!(wrapped[3].to_string(), "should");
        assert_eq!(wrapped[4].to_string(), "wrap");
    }

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