use crate::error::AzUserUsageError;

#[derive(Debug, PartialEq, Clone, Hash)]
pub enum SHPartialKeyPart {
    Name(String),
    /// limit the index to a small range less than 65535
    Index(u16),
    /// the index is beyond the end of the array, the final index is the sum of the last element index and the append index value
    AppendIndex(u16),
}

#[derive(Debug, PartialEq, Clone)]
pub struct SHPartialKeys {
    parts: Vec<SHPartialKeyPart>,
}

impl SHPartialKeys {
    pub fn new(parts: impl IntoIterator<Item = SHPartialKeyPart>) -> Self {
        Self {
            parts: parts.into_iter().collect(),
        }
    }

    pub fn from_parts<'a>(parts: impl IntoIterator<Item = &'a SHPartialKeyPart>) -> Self {
        Self::new(parts.into_iter().map(|p| p.to_owned()))
    }
}

impl Default for SHPartialKeys {
    fn default() -> Self {
        Self { parts: vec![] }
    }
}

impl SHPartialKeys {
    pub fn push(&mut self, part: SHPartialKeyPart) {
        self.parts.push(part);
    }

    pub fn extend<'a>(&mut self, other: impl IntoIterator<Item = &'a SHPartialKeyPart>) {
        self.parts.extend(other.into_iter().map(|p| p.to_owned()));
    }

    /// create a new SHPartialKeys with the extended parts
    pub fn with_extend<'a>(&self, other: impl IntoIterator<Item = &'a SHPartialKeyPart>) -> Self {
        let mut result = self.clone();
        result.extend(other);
        result
    }
}

impl std::fmt::Display for SHPartialKeys {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let mut first = true;
        for part in self.parts.iter() {
            match part {
                SHPartialKeyPart::Name(_) => {
                    if first {
                        write!(f, "{}", part)?;
                        first = false;
                    } else {
                        write!(f, ".{}", part)?;
                    }
                }
                _ => write!(f, "{}", part)?,
            }
        }
        Ok(())
    }
}

impl std::fmt::Display for SHPartialKeyPart {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SHPartialKeyPart::Name(name) => write!(f, "{}", name),
            SHPartialKeyPart::Index(idx) => write!(f, "[{}]", idx),
            SHPartialKeyPart::AppendIndex(idx) => write!(f, "[+{}]", idx),
        }
    }
}

impl SHPartialKeys {
    pub fn is_empty(&self) -> bool {
        self.parts.is_empty()
    }

    pub fn parts(&self) -> impl Iterator<Item = &SHPartialKeyPart> {
        self.parts.iter()
    }
}

impl SHPartialKeyPart {
    pub fn parse_name(value: &str) -> Result<Self, AzUserUsageError> {
        if value.len() == 0 {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: value.to_string(),
                at: 0,
                range: value.len(),
            });
        }
        Ok(SHPartialKeyPart::Name(value.to_string()))
    }

    pub fn parse_index(value: &str) -> Result<Self, AzUserUsageError> {
        match value.parse::<u64>() {
            Ok(idx) => {
                if idx > u16::MAX as u64 {
                    return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg: "Index out of range, should be less than 65535".to_string(),
                        data: value.to_string(),
                        at: 0,
                        range: value.len(),
                    });
                }
                if value.starts_with("+") {
                    Ok(SHPartialKeyPart::AppendIndex(idx as u16))
                } else {
                    Ok(SHPartialKeyPart::Index(idx as u16))
                }
            }
            Err(_) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Invalid index, support integer for direct index or integer with + for append index.".to_string(),
                data: value.to_string(),
                at: 0,
                range: value.len(),
            }),
        }
    }
}
