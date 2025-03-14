use std::str::{Chars, FromStr};
use std::sync::LazyLock;

use log::{debug, info};
use regex::Regex;
use serde::de::value;
use serde_json::map::IntoIter;
use serde_json::{json, Map, Value};

use crate::error::{AzInternalError, AzResult, AzUserUsageError};
use crate::pb::command::cmd_arg::ArgType;
use crate::pb::command::{self, CmdArgEnum, CmdArgEnumItem};

#[derive(Debug, PartialEq, Clone)]
pub enum SHPartialKeyPart {
    Name(String),
    /// limit the index to a small range less than 65535
    Index(u16),
    /// the index is beyond the end of the array, the final index is the sum of the last element index and the append index value
    AppendIndex(u16),
}

#[derive(Debug, PartialEq, Clone)]
pub struct SHPartialKeys {
    pub(super) parts: Vec<SHPartialKeyPart>,
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

    pub fn extend(&mut self, other: impl IntoIterator<Item = SHPartialKeyPart>) {
        self.parts.extend(other);
    }

    pub fn with_extend(mut self, other: impl IntoIterator<Item = SHPartialKeyPart>) -> Self {
        self.extend(other);
        self
    }
}

impl IntoIterator for SHPartialKeys {
    type Item = SHPartialKeyPart;
    type IntoIter = std::vec::IntoIter<SHPartialKeyPart>;

    fn into_iter(self) -> Self::IntoIter {
        self.parts.into_iter()
    }
}

impl std::fmt::Display for SHPartialKeys {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let mut first = true;
        for part in self.parts.iter() {
            match part {
                SHPartialKeyPart::Name(name) => {
                    if first {
                        write!(f, "{}", name)?;
                        first = false;
                    } else {
                        write!(f, ".{}", name)?;
                    }
                }
                SHPartialKeyPart::Index(idx) => write!(f, "[{}]", idx)?,
                SHPartialKeyPart::AppendIndex(idx) => write!(f, "[+{}]", idx)?,
            }
        }
        Ok(())
    }
}

impl SHPartialKeys {
    pub fn split_at(&self, at: usize) -> Self {
        let mut result = Vec::new();
        for (i, part) in self.parts.iter().enumerate() {
            if i >= at {
                break;
            }
            result.push(part.to_owned());
        }
        Self { parts: result }
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
