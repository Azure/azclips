use std::{
    fmt::Display,
    mem,
    ops::{Add, AddAssign},
};

use crossterm::style::StyledContent;
use unicode_width::UnicodeWidthStr;

pub use crossterm::style::{Attribute, Color, Stylize};

#[derive(Debug, Clone, Default)]
pub struct StyledString(Vec<StyledContent<String>>);

impl StyledString {
    pub fn new() -> Self {
        Self::default()
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

    pub fn split_lines(self) -> Vec<Self> {
        let mut result = vec![];
        let mut cur_line = StyledString(vec![]);
        for part in self.0 {
            let lines = part.content().lines().collect::<Vec<_>>();
            if lines.len() > 1 {
                cur_line.append(StyledContent::new(
                    part.style().clone(),
                    lines[0].to_string(),
                ));
                for line in &lines[1..] {
                    cur_line.optimize_style();
                    result.push(cur_line);
                    cur_line = StyledString(vec![StyledContent::new(
                        part.style().clone(),
                        line.to_string(),
                    )]);
                }
            } else {
                cur_line.append(part);
            }
        }
        if !cur_line.is_empty() {
            cur_line.optimize_style();
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
                let content = part.content();
                
                // Split by whitespace but keep the whitespace
                let mut parts = Vec::new();
                let mut current_word = String::new();
                let mut current_spaces = String::new();
                let mut in_word = false;
                
                for ch in content.chars() {
                    if ch.is_whitespace() {
                        if in_word {
                            // Finished a word, save it
                            if !current_word.is_empty() {
                                parts.push((current_word.clone(), false));
                                current_word.clear();
                            }
                            in_word = false;
                        }
                        current_spaces.push(ch);
                    } else {
                        if !in_word {
                            // Finished spaces, save them
                            if !current_spaces.is_empty() {
                                parts.push((current_spaces.clone(), true));
                                current_spaces.clear();
                            }
                            in_word = true;
                        }
                        current_word.push(ch);
                    }
                }
                
                if !current_word.is_empty() {
                    parts.push((current_word, false));
                }
                if !current_spaces.is_empty() {
                    parts.push((current_spaces, true));
                }
                
                for (text, is_whitespace) in parts {
                    let text_width = text.width_cjk();
                    
                    if cur_width + text_width > limit && cur_width > 0 {
                        cur_line.optimize_style();
                        result.push(cur_line);
                        cur_line = StyledString(vec![]);
                        cur_width = 0;
                        
                        if is_whitespace {
                            continue;
                        }
                    }
                    
                    cur_line.append(StyledContent::new(part.style().clone(), text));
                    cur_width += text_width;
                    //TODO: handle the case where the text is too long to fit in a single line
                }
            }
            
            if !cur_line.is_empty() {
                cur_line.optimize_style();
                result.push(cur_line);
            }
            result
        } else {
            vec![self.clone()]
        }
    }

    pub fn soft_wrap(self, width: Option<usize>) -> Vec<Self> {
        self.split_lines()
            .into_iter()
            .flat_map(|line| line.soft_wrap_paragraph(width))
            .collect()
    }

    fn optimize_style(&mut self) {
        let old = mem::replace(&mut self.0, vec![]);
        for part in old {
            if let Some(last) = self.0.last_mut() {
                if last.style() == part.style() {
                    *last = StyledContent::new(
                        last.style().clone(),
                        format!("{}{}", last.content(), part.content()),
                    );
                } else {
                    self.0.push(part);
                }
            } else {
                self.0.push(part);
            }
        }
    }
}

impl From<StyledContent<String>> for StyledString {
    fn from(value: StyledContent<String>) -> Self {
        StyledString(vec![value])
    }
}

impl From<StyledContent<&str>> for StyledString {
    fn from(value: StyledContent<&str>) -> Self {
        StyledString(vec![StyledContent::new(
            *value.style(),
            value.content().to_string(),
        )])
    }
}

impl From<String> for StyledString {
    fn from(value: String) -> Self {
        StyledString(vec![value.stylize()])
    }
}

impl From<&String> for StyledString {
    fn from(value: &String) -> Self {
        StyledString(vec![value.clone().stylize()])
    }
}

impl From<&str> for StyledString {
    fn from(value: &str) -> Self {
        StyledString(vec![value.to_string().stylize()])
    }
}

impl Add for StyledString {
    type Output = Self;

    fn add(mut self, rhs: Self) -> Self::Output {
        self.0.extend(rhs.0);
        self
    }
}

impl<T> Add<StyledContent<T>> for StyledString
where
    T: AsRef<str> + std::fmt::Display,
{
    type Output = Self;

    fn add(mut self, rhs: StyledContent<T>) -> Self::Output {
        let content = StyledContent::new(rhs.style().clone(), rhs.content().to_string());
        self.0.push(content);
        self
    }
}

impl Add<String> for StyledString {
    type Output = Self;

    fn add(mut self, rhs: String) -> Self::Output {
        self.0.push(rhs.stylize());
        self
    }
}

impl Add<&String> for StyledString {
    type Output = Self;

    fn add(self, rhs: &String) -> Self::Output {
        self + rhs.clone()
    }
}

impl Add<&str> for StyledString {
    type Output = Self;

    fn add(mut self, rhs: &str) -> Self::Output {
        self.0.push(rhs.to_string().stylize());
        self
    }
}

impl AddAssign for StyledString {
    fn add_assign(&mut self, rhs: Self) {
        self.0.extend(rhs.0);
    }
}

impl<T> AddAssign<StyledContent<T>> for StyledString
where
    T: AsRef<str> + std::fmt::Display,
{
    fn add_assign(&mut self, rhs: StyledContent<T>) {
        let content = StyledContent::new(rhs.style().clone(), rhs.content().to_string());
        self.0.push(content);
    }
}

impl AddAssign<String> for StyledString {
    fn add_assign(&mut self, rhs: String) {
        self.0.push(rhs.stylize());
    }
}

impl AddAssign<&String> for StyledString {
    fn add_assign(&mut self, rhs: &String) {
        self.0.push(rhs.clone().stylize());
    }
}

impl AddAssign<&str> for StyledString {
    fn add_assign(&mut self, rhs: &str) {
        self.0.push(rhs.to_string().stylize());
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
            }
            Some(std::fmt::Alignment::Center) => {
                let left = (width - len) / 2;
                let right = width - len - left;
                write!(f, "{}", fill.to_string().repeat(left))?;
                for part in &self.0 {
                    write!(f, "{}", part)?;
                }
                write!(f, "{}", fill.to_string().repeat(right))?;
            }
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

#[cfg(test)]
mod tests {
    use super::*;
    use crossterm::style::ContentStyle;

    #[test]
    fn test_styled_string_display() {
        let styled_content1 = "Hello".to_string().stylize();
        let styled_content2 = " World".to_string().stylize();
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
            "This is a longer text that should wrap".to_string(),
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
}
