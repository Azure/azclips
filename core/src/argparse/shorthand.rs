use std::str::{Chars, FromStr};
use std::sync::LazyLock;

use log::{debug, info};
use regex::Regex;
use serde::de::value;
use serde_json::map::IntoIter;
use serde_json::{json, Map, Value};

use crate::error::{AzInternalError, AzResult, AzUserUsageError};
use crate::pb::command::cmd_arg::ArgType;

use super::partial::{SHPartialKeyPart, SHPartialKeys};

pub struct ShorthandSyntaxParser {
    null_expr: &'static str,
    help_expr: &'static str,
    single_quote_char: char,
    double_quote_char: char,
    quote_escape_char: char,
    dict_start_char: char,
    dict_end_char: char,
    list_start_char: char,
    list_end_char: char,
    key_value_separator: char,
    prop_name_separator: char,
    item_separator: char,
    partial_value_key_pattern: Regex,
}

pub(super) static SHORTHAND_SYNTAX_PARSER: LazyLock<ShorthandSyntaxParser> =
    LazyLock::new(ShorthandSyntaxParser::default);

/// The regex pattern for the partial value key, which is used to parse the partial value key from the input string.
const PARTIAL_VALUE_KEY_PATTERN: &'static str = concat!(
    "^",                               // start
    "(",                               // start of capture group 1 - key path
    "(?:",                             // non-capturing group - first key
    "(?:[a-zA-Z0-9_\\-]+)",            // normal identifier
    "|",                               // or
    "(?:'(?:[^']*)'(?:/(?:[^']*)')*)", // quoted key
    "|",                               // or
    "(?:\\[(?:(?:-|\\+)?[0-9]*)\\])", // array index, in the expression, match the [] and [-1], but raise error when part key, the index support [5], [+5] (for beyond the end of the array, +5 represent the index which is last element index of array plus 5)
    ")",
    "(?:\\[(?:(?:-|\\+)?[0-9]*)\\])*", // optional array index, in the expression, match the [] and [-1], but raise error when part key, the index support [5], [+5] (for beyond the end of the array, +5 represent the index which is last element index of array plus 5)
    "(?:",                             // non-capturing group - subsequent keys
    "\\.",                             // dot separator
    "(?:",                             // non-capturing group
    "(?:[a-zA-Z0-9_\\-]*)", // normal identifier, in the expression, match the ., but raise error when part key
    "|",                    // or
    "(?:'(?:[^']*)'(?:/(?:[^']*)')*)", // quoted key
    ")",
    "(?:\\[(?:(?:-|\\+)?[0-9]*)\\])*", // optional array index, in the expression, match the [] and [-1], but raise error when part key, the index support [5], [+5] (for beyond the end of the array, +5 represent the index which is last element index of array plus 5)
    ")*",                              // repeat zero or more times
    ")",                               // end of capture group 1
    "=",                               // equals separator
    "(.*)",                            // capture group 2 - value part
    "$"                                // end
);

impl Default for ShorthandSyntaxParser {
    fn default() -> Self {
        ShorthandSyntaxParser {
            null_expr: "null",
            help_expr: "??",
            single_quote_char: '\'',
            double_quote_char: '"',
            quote_escape_char: '/',
            dict_start_char: '{',
            dict_end_char: '}',
            list_start_char: '[',
            list_end_char: ']',
            key_value_separator: ':',
            prop_name_separator: '.',
            item_separator: ',',
            partial_value_key_pattern: Regex::new(PARTIAL_VALUE_KEY_PATTERN).unwrap(),
        }
    }
}

/// The result of the shorthand syntax parser, which is used to parse the shorthand syntax.
#[derive(Debug, PartialEq)]
pub enum SHResult {
    /// contains the index keys for the help message in reverse order, should convert to the normal order in the caller
    Help(Vec<SHPartialKeyPart>),
    Value(Value),
}

/// Read the value from the file, and return the value if the file is a valid serde value.
/// If the file is not a valid serde value, return None
/// If failed to read the file, return an error
fn read_serde_value_from_file(file_path: &str) -> AzResult<Option<Value>> {
    if file_path.ends_with(".json") {
        debug!("Attempting to read from json file {}", file_path);
        if let Ok(content) = std::fs::read_to_string(file_path) {
            let value = serde_json::from_str(&content).map_err(|e| {
                AzUserUsageError::ParseFileError(file_path.to_string(), e.to_string())
            })?;
            return Ok(Some(value));
        }
        debug!(
            "Failed to read from json file {}, assume it's a simple string value",
            file_path
        );
    } else if file_path.ends_with(".yaml") || file_path.ends_with(".yml") {
        debug!("Attempting to read from yaml file: {}", file_path);
        if let Ok(_) = std::fs::read_to_string(file_path) {
            // TODO: support yaml file
            // not support yaml file yet, the serde_yaml is not maintained,
            // the https://github.com/Ethiraric/yaml-rust2 is not compatible with the serde
            // need the additional wrapper to make it compatible with the serde
            return Err(AzUserUsageError::UnsupportedFeatureError(format!(
                "yaml file input is not supported yet, please use json file instead. {}",
                file_path
            ))
            .into());
        }
        debug!(
            "Failed to read from yaml file {}, assume it's a simple string value",
            file_path
        );
    } else {
        debug!(
            "Failed to match file type {}, assume it's a simple string value",
            file_path
        );
    }
    Ok(None)
}

impl ShorthandSyntaxParser {
    pub(super) fn parse(
        &self,
        value: &str,
        simple_type_only: bool,
    ) -> Result<SHResult, AzUserUsageError> {
        if simple_type_only {
            if *value == *self.null_expr {
                Ok(SHResult::Value(Value::Null))
            } else if *value == *self.help_expr {
                Ok(SHResult::Help(vec![]))
            } else if !value.is_empty() && value.starts_with(self.single_quote_char) {
                let mut remain = value.chars();
                let (result, len) = self.parse_single_quotes_string(&mut remain)?;
                if remain.as_str().len() > 0 {
                    Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg: "Redundant tail for quota expression".to_string(),
                        data: value.to_string(),
                        at: len,
                        range: remain.count(),
                    })
                } else {
                    Ok(SHResult::Value(Value::String(result)))
                }
            } else {
                // return the bool, number, or string value directly
                if let Ok(v) = serde_json::from_str::<Value>(value) {
                    match v {
                        Value::Bool(v) => Ok(SHResult::Value(Value::Bool(v))),
                        Value::Number(v) => Ok(SHResult::Value(Value::Number(v))),
                        _ => Ok(SHResult::Value(Value::String(value.to_string()))),
                    }
                } else {
                    Ok(SHResult::Value(Value::String(value.to_string())))
                }
            }
        } else {
            let mut remain = value.chars();
            let (result, len) = self.parse_value(&mut remain)?;
            if remain.as_str().len() > 0 {
                Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Redundant tail".to_string(),
                    data: value.to_string(),
                    at: len,
                    range: remain.count(),
                })
            } else {
                Ok(result)
            }
        }
    }

    fn parse_single_quotes_string(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(String, usize), AzUserUsageError> {
        let data = remain.as_str();
        assert!(remain.next().unwrap() == self.single_quote_char);
        let mut idx = 1;
        let mut result: String = String::new();
        while let Some(c) = remain.next() {
            if c == self.single_quote_char {
                let str = remain.as_str();
                if str.starts_with(self.quote_escape_char) {
                    // the quote is escaped, so not end of the quote
                    _ = remain.next().unwrap();
                    result.push(c);
                    idx += 2;
                } else {
                    idx += 1;
                    return Ok((result, idx));
                }
            } else {
                result.push(c);
                idx += 1;
            }
        }

        Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: format!(
                "Miss the end single quote character: {}",
                self.single_quote_char
            ),
            data: data.to_string(),
            at: idx,
            range: 1,
        })
    }

    fn parse_value(&self, remain: &mut Chars<'_>) -> Result<(SHResult, usize), AzUserUsageError> {
        let str = remain.as_str();
        if str.starts_with(self.dict_start_char) {
            self.parse_dict(remain)
        } else if str.starts_with(self.list_start_char) {
            self.parse_list(remain)
        } else {
            self.parse_string(remain, true)
        }
    }

    fn parse_dict(&self, remain: &mut Chars<'_>) -> Result<(SHResult, usize), AzUserUsageError> {
        let data = remain.as_str();
        let c = remain.next().unwrap();
        assert!(c == self.dict_start_char);
        let mut idx = 1;
        let mut str = remain.as_str();
        let mut result = Map::new();

        while str.len() > 0 {
            if str.starts_with(self.dict_end_char) {
                _ = remain.next().unwrap();
                idx += 1;
                return Ok((SHResult::Value(Value::Object(result)), idx));
            }

            let (k, len) = self.parse_string(remain, false).map_err(|e| match e {
                AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: _,
                    at,
                    range,
                } => AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: data.to_string(),
                    at: at + idx,
                    range,
                },
                e => e,
            })?;

            let k = match k {
                SHResult::Value(value) => value.as_str().unwrap().to_string(),
                SHResult::Help(keys) => {
                    return Ok((SHResult::Help(keys), idx));
                }
            };

            if k.contains(self.double_quote_char) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Dict key should not contain double quotes".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: len,
                });
            }

            if k.contains(self.single_quote_char) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Dict key should not contain single quotes".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: len,
                });
            }

            if result.contains_key(&k) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: format!("Duplicate defined dict key '{}'", k),
                    data: data.to_string(),
                    at: idx,
                    range: len,
                });
            }

            idx += len;
            str = remain.as_str();

            if !str.starts_with(self.key_value_separator) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Expect character ':'".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                });
            }
            idx += 1;
            _ = remain.next().unwrap();
            str = remain.as_str();

            if str.len() == 0 {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Cannot parse empty".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                });
            }

            let (v, len) = self.parse_value(remain).map_err(|e| match e {
                AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: _,
                    at,
                    range,
                } => AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: data.to_string(),
                    at: at + idx,
                    range,
                },
                e => e,
            })?;

            match v {
                SHResult::Help(mut keys) => {
                    keys.push(SHPartialKeyPart::parse_name(&k)?);
                    return Ok((SHResult::Help(keys), idx));
                }
                SHResult::Value(value) => {
                    result.insert(k, value);
                    idx += len;
                    str = remain.as_str();
                }
            };

            if str.starts_with(self.item_separator) {
                _ = remain.next().unwrap();
                idx += 1;
                str = remain.as_str();
            } else if !str.starts_with(self.dict_end_char) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Expect character ',' or '}'".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                });
            }
        }

        Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: "Expect character '}'".to_string(),
            data: data.to_string(),
            at: idx,
            range: 1,
        })
    }

    fn parse_list(&self, remain: &mut Chars<'_>) -> Result<(SHResult, usize), AzUserUsageError> {
        let data = remain.as_str();
        let c = remain.next().unwrap();
        assert!(c == self.list_start_char);
        let mut idx = 1;
        let mut str = remain.as_str();
        let mut result = Vec::new();

        while str.len() > 0 {
            if str.starts_with(self.list_end_char) {
                _ = remain.next().unwrap();
                idx += 1;
                return Ok((SHResult::Value(Value::Array(result)), idx));
            }

            let (v, len) = self.parse_value(remain).map_err(|e| match e {
                AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: _,
                    at,
                    range,
                } => AzUserUsageError::InvalidShorthandSyntaxError {
                    msg,
                    data: data.to_string(),
                    at: at + idx,
                    range,
                },
                e => e,
            })?;
            match v {
                SHResult::Help(mut keys) => {
                    keys.push(SHPartialKeyPart::Index(result.len() as u16));
                    return Ok((SHResult::Help(keys), idx));
                }
                SHResult::Value(value) => {
                    result.push(value);
                    idx += len;
                    str = remain.as_str();
                }
            }
            if str.starts_with(self.item_separator) {
                _ = remain.next().unwrap();
                idx += 1;
                str = remain.as_str();
            } else if !str.starts_with(self.list_end_char) {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Expect character ',' or ']'".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                });
            }
        }

        Err(AzUserUsageError::InvalidShorthandSyntaxError {
            msg: "Expect character ']'".to_string(),
            data: data.to_string(),
            at: idx,
            range: 1,
        })
    }

    fn parse_string(
        &self,
        remain: &mut Chars<'_>,
        convert_simple_type: bool, // whether to convert the string into the number, boolean type.
    ) -> Result<(SHResult, usize), AzUserUsageError> {
        let data = remain.as_str();
        let mut str = remain.as_str();
        if str.starts_with(self.single_quote_char) {
            let (result, len) = self.parse_single_quotes_string(remain)?;
            return Ok((SHResult::Value(Value::String(result)), len));
        }

        let mut idx = 0;
        let mut result = String::new();
        while str.len() > 0 {
            // let str = remain.as_str();
            if str.starts_with(self.dict_end_char)
                || str.starts_with(self.list_end_char)
                || str.starts_with(self.item_separator)
                || str.starts_with(self.key_value_separator)
            {
                break;
            }

            let c = remain.next().unwrap();
            if c.is_whitespace() {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: "Please wrap whitespace character in single quotes".to_string(),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                });
            }
            if c == self.single_quote_char || c == self.double_quote_char {
                return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                    msg: format!("Please wrap '{}' character in single quotes", c),
                    data: data.to_string(),
                    at: idx,
                    range: 1,
                });
            }

            idx += 1;
            result.push(c);
            str = remain.as_str();
        }

        if idx == 0 {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Cannot parse empty, please use '' wrapped empty string.".to_string(),
                data: data.to_string(),
                at: idx,
                range: 1,
            });
        }

        if result == self.null_expr {
            Ok((SHResult::Value(Value::Null), idx))
        } else if result == self.help_expr {
            Ok((SHResult::Help(vec![]), idx))
        } else if convert_simple_type {
            match serde_json::from_str::<Value>(&result) {
                Ok(Value::Bool(v)) => Ok((SHResult::Value(Value::Bool(v)), idx)),
                Ok(Value::Number(v)) => Ok((SHResult::Value(Value::Number(v)), idx)),
                _ => Ok((SHResult::Value(Value::String(result)), idx)),
            }
        } else {
            Ok((SHResult::Value(Value::String(result)), idx))
        }
    }

    /// split the partial value into the key and the value
    /// when the value is empty, return None to represent it's a blank value
    pub(super) fn split_partial_value<'a>(
        &self,
        value: &'a str,
    ) -> Option<(&'a str, Option<&'a str>)> {
        if let Some(captures) = self.partial_value_key_pattern.captures(value) {
            let key = captures.get(1).unwrap().as_str();
            let value = captures.get(2).unwrap().as_str();
            if value.is_empty() {
                // the value is empty, so it is a blank value
                Some((key, None))
            } else {
                Some((key, Some(value)))
            }
        } else {
            None
        }
    }

    pub(super) fn parse_partial_value_key_parts(
        &self,
        key: &str,
    ) -> Result<SHPartialKeys, AzUserUsageError> {
        let mut keys = SHPartialKeys::default();
        let mut remain = key.chars();
        let mut idx = 0;
        let mut str = remain.as_str();
        while str.len() > 0 {
            if str.starts_with(self.list_start_char) {
                match self.parse_partial_value_idx_key(&mut remain) {
                    Ok((part, len)) => {
                        keys.parts.push(part);
                        idx += len;
                        str = remain.as_str();
                    }
                    Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    }) => {
                        return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data: key.to_string(),
                            at: at + idx,
                            range,
                        })
                    }
                    Err(e) => return Err(e),
                }
            } else {
                match self.parse_partial_value_prop_key(&mut remain) {
                    Ok((part, len)) => {
                        keys.parts.push(part);
                        idx += len;
                        str = remain.as_str();
                    }
                    Err(AzUserUsageError::InvalidShorthandSyntaxError {
                        msg,
                        data: _,
                        at,
                        range,
                    }) => {
                        return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                            msg,
                            data: key.to_string(),
                            at: at + idx,
                            range,
                        })
                    }
                    Err(e) => return Err(e),
                }
            }
        }

        Ok(keys)
    }

    fn parse_partial_value_idx_key(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(SHPartialKeyPart, usize), AzUserUsageError> {
        let data = remain.as_str();
        assert_eq!(remain.next().unwrap(), self.list_start_char);
        let mut str = remain.as_str();
        let mut idx = 1;

        let mut part = String::new();
        while str.len() > 0 {
            if str.starts_with(self.list_end_char) || str.starts_with(self.prop_name_separator) {
                break;
            }
            part.push(remain.next().unwrap());
            idx += 1;
            str = remain.as_str();
        }

        if !str.starts_with(self.list_end_char) {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ']'".to_string(),
                data: data.to_string(),
                at: idx,
                range: 1,
            });
        }
        _ = remain.next().unwrap();
        // str = remain.as_str();
        idx += 1;
        if part.is_empty() {
            return Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: data.to_string(),
                at: 0,
                range: idx,
            });
        }
        match SHPartialKeyPart::parse_index(&part) {
            Ok(part) => Ok((part, idx)),
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: _,
                at: _,
                range: _,
            }) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: data.to_string(),
                at: 0,
                range: idx,
            }),
            Err(e) => Err(e),
        }
    }

    fn parse_partial_value_prop_key(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(SHPartialKeyPart, usize), AzUserUsageError> {
        let data = remain.as_str();
        let mut str = remain.as_str();
        let mut idx = 0;
        if str.starts_with(self.prop_name_separator) {
            _ = remain.next().unwrap();
            idx += 1;
            str = remain.as_str();
        }
        let part = if str.starts_with(self.single_quote_char) {
            let (part, len) = self.parse_single_quotes_string(remain)?;
            idx += len;
            str = remain.as_str();
            part
        } else {
            let mut part = String::new();
            while str.len() > 0 {
                if str.starts_with(self.prop_name_separator)
                    || str.starts_with(self.list_start_char)
                {
                    break;
                }
                part.push(remain.next().unwrap());
                idx += 1;
                str = remain.as_str();
            }
            part
        };

        match SHPartialKeyPart::parse_name(&part) {
            Ok(part) => Ok((part, idx)),
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: _,
                at: _,
                range: _,
            }) => Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg,
                data: data.to_string(),
                at: 0,
                range: idx,
            }),
            Err(e) => Err(e),
        }
    }

    // fn parse_complex_arg_value(value: &str)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_simple_value_null() {
        let parser = ShorthandSyntaxParser::default();

        let result = parser.parse("null", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::Null));

        let result = parser.parse("Null", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("Null".to_string())));

        let result = parser.parse("NULL", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("NULL".to_string())));

        let result = parser.parse("'null'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("null".to_string())));

        let result = parser.parse("'null'abc", false);

        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Redundant tail".to_string(),
                data: "'null'abc".to_string(),
                at: 6,
                range: 3,
            })
        );
    }

    #[test]
    fn test_parse_simple_value_bool() {
        let parser = ShorthandSyntaxParser::default();

        let result = parser.parse("true", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::Bool(true)));
        let result = parser.parse("false", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::Bool(false)));
        let result = parser.parse("True", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("True".to_string())));
        let result = parser.parse("False", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("False".to_string())));
        let result = parser.parse("'true'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("true".to_string())));
        let result = parser.parse("'false'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("false".to_string())));

        let result = parser.parse("'true'abc", false);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Redundant tail".to_string(),
                data: "'true'abc".to_string(),
                at: 6,
                range: 3,
            })
        );
    }

    #[test]
    fn test_parse_simple_value_number() {
        let parser = ShorthandSyntaxParser::default();

        let result = parser.parse("0", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(serde_json::Number::from(0)))
        );
        let result = parser.parse("0.0", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(serde_json::Number::from_f64(0.0).unwrap()))
        );

        let result = parser.parse("123", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(serde_json::Number::from(123)))
        );
        let result = parser.parse("-123", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(serde_json::Number::from(-123)))
        );
        let result = parser.parse("123.456", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(
                serde_json::Number::from_f64(123.456).unwrap()
            ))
        );
        let result = parser.parse("-123.456", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(
                serde_json::Number::from_f64(-123.456).unwrap()
            ))
        );

        let result = parser.parse("'0'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("0".to_string())));

        let result = parser.parse("'0.0'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("0.0".to_string())));

        let result = parser.parse("'123'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("123".to_string())));
        let result = parser.parse("'-123'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("-123".to_string())));

        let result = parser.parse("'123.456'", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("123.456".to_string()))
        );
        let result = parser.parse("'-123.456'", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("-123.456".to_string()))
        );

        let result = parser.parse("'123.456'abc", false);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Redundant tail".to_string(),
                data: "'123.456'abc".to_string(),
                at: 9,
                range: 3,
            })
        );
    }

    #[test]
    fn test_parse_simple_value_string() {
        let parser = ShorthandSyntaxParser::default();

        let result = parser.parse("'hello'", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("hello".to_string())));

        let result = parser.parse("'hello world'", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("hello world".to_string()))
        );

        let result = parser.parse("hello world", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("hello world".to_string()))
        );

        let result = parser.parse("{a:b}", true).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("{a:b}".to_string())));

        let result = parser.parse("{\"a\":\"b\"}", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("{\"a\":\"b\"}".to_string()))
        );

        let result = parser.parse("[1, 2, 3]", true).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::String("[1, 2, 3]".to_string()))
        );

        let result = parser.parse("'hello world'abc", false);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Redundant tail".to_string(),
                data: "'hello world'abc".to_string(),
                at: 13,
                range: 3,
            })
        );
    }

    #[test]
    fn test_parse_single_quote_string() {
        let parser = ShorthandSyntaxParser::default();

        let mut remain = "'hello world'".chars();
        let result = parser.parse_single_quotes_string(&mut remain).unwrap();
        assert_eq!(result, ("hello world".to_string(), 13));

        assert_eq!(remain.as_str(), "");

        let mut remain = "'hello world'abc".chars();
        let result = parser.parse_single_quotes_string(&mut remain).unwrap();
        assert_eq!(result, ("hello world".to_string(), 13));

        assert_eq!(remain.as_str(), "abc");

        let mut remain = "'hello world'/ab/c//'".chars();
        let result = parser.parse_single_quotes_string(&mut remain).unwrap();
        assert_eq!(result, ("hello world'ab/c//".to_string(), 21));
        assert_eq!(remain.as_str(), "");

        // error cases
        let mut remain = "'hello world".chars();
        let r = parser.parse_single_quotes_string(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss the end single quote character: '".to_string(),
                data: "'hello world".to_string(),
                at: 12,
                range: 1,
            })
        );

        let mut remain = "'hello world'/abc".chars();
        let r = parser.parse_single_quotes_string(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss the end single quote character: '".to_string(),
                data: "'hello world'/abc".to_string(),
                at: 17,
                range: 1,
            })
        );
    }

    #[test]
    fn test_parse_not_sample_value() {
        let parser = ShorthandSyntaxParser::default();

        let result = parser.parse("abc", false).unwrap();
        assert_eq!(result, SHResult::Value(Value::String("abc".to_string())));

        let result = parser.parse("123", false).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Number(serde_json::Number::from(123)))
        );

        let result = parser.parse("true", false).unwrap();
        assert_eq!(result, SHResult::Value(Value::Bool(true)));

        let result = parser.parse("false", false).unwrap();
        assert_eq!(result, SHResult::Value(Value::Bool(false)));

        let result = parser.parse("null", false).unwrap();
        assert_eq!(result, SHResult::Value(Value::Null));

        let result = parser.parse("{a:b}", false).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Object(
                json!({"a": "b"}).as_object().unwrap().clone()
            ))
        );

        let result = parser.parse("[1,2,3]", false).unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Array(json!([1, 2, 3]).as_array().unwrap().clone()))
        );

        let result = parser
            .parse(
                "{windows:'c:\\temp\\filter.json',unix1:'/tmp/filter.json',unix2:/var/tmp/filter.json}",
                false,
            )
            .unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Object(
                json!({"windows":"c:\\temp\\filter.json","unix1":"/tmp/filter.json","unix2":"/var/tmp/filter.json"})
                    .as_object()
                    .unwrap()
                    .clone()
            ))
        );

        let result = parser
            .parse(
                "{a:1,c:d,l1:['1','2','3'],l2:[{a:b,c:d},{e:f},null,'null',true,false],str1:'[1,2,3]',str2:'{a:b,c:d}'}",
                false,
            )
            .unwrap();
        assert_eq!(
            result,
            SHResult::Value(Value::Object(
                json!({
                    "a": 1,
                    "c": "d",
                    "l1": ["1", "2", "3"],
                    "l2": [
                        {"a": "b", "c": "d"},
                        {"e": "f"},
                        null,
                        "null",
                        true,
                        false,
                    ],
                    "str1":"[1,2,3]",
                    "str2":"{a:b,c:d}",
                })
                .as_object()
                .unwrap()
                .clone()
            ))
        );

        // test error cases
        let r = parser.parse("{a:1,c:2},3", false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Redundant tail".to_string(),
                data: "{a:1,c:2},3".to_string(),
                at: 9,
                range: 2,
            })
        );
    }

    #[test]
    fn test_parse_string_convert_simple_type() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "abc".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 3)
        );

        let mut remain = "'abc'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 5)
        );

        let mut remain = "123".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Number(serde_json::Number::from(123))),
                3
            )
        );

        let mut remain = "'123'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("123".to_string())), 5)
        );

        let mut remain = "true".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(result, (SHResult::Value(Value::Bool(true)), 4));

        let mut remain = "'true'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("true".to_string())), 6)
        );

        let mut remain = "false".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(result, (SHResult::Value(Value::Bool(false)), 5));

        let mut remain = "'false'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("false".to_string())), 7)
        );

        let mut remain = "null".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(result, (SHResult::Value(Value::Null), 4));

        let mut remain = "'null'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("null".to_string())), 6)
        );

        let mut remain = "'{\"a\":\"b\"}'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::String("{\"a\":\"b\"}".to_string())),
                11
            )
        );

        let mut remain = "'[1,2,3]'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("[1,2,3]".to_string())), 9)
        );
    }

    #[test]
    fn test_parse_string_not_convert_simple_type() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "abc".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 3)
        );

        let mut remain = "'abc'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 5)
        );

        let mut remain = "123".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("123".to_string())), 3)
        );

        let mut remain = "'123'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("123".to_string())), 5)
        );

        let mut remain = "true".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("true".to_string())), 4)
        );

        let mut remain = "'true'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("true".to_string())), 6)
        );

        let mut remain = "false".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("false".to_string())), 5)
        );

        let mut remain = "'false'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("false".to_string())), 7)
        );

        let mut remain = "null".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(result, (SHResult::Value(Value::Null), 4));

        let mut remain = "'null'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("null".to_string())), 6)
        );

        let mut remain = "'{\"a\":\"b\"}'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::String("{\"a\":\"b\"}".to_string())),
                11
            )
        );

        let mut remain = "'[1,2,3]'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("[1,2,3]".to_string())), 9)
        );
    }

    #[test]
    fn test_parse_string_part() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "abc,def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 3)
        );
        assert_eq!(remain.as_str(), ",def");
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "'a b c',def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("a b c".to_string())), 7)
        );
        assert_eq!(remain.as_str(), ",def");

        let mut remain = "abc:def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 3)
        );
        assert_eq!(remain.as_str(), ":def");

        let mut remain = "abc],def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 3)
        );
        assert_eq!(remain.as_str(), "],def");

        let mut remain = "abc},def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (SHResult::Value(Value::String("abc".to_string())), 3)
        );
        assert_eq!(remain.as_str(), "},def");

        let mut remain = "'',def,ghi".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(result, (SHResult::Value(Value::String("".to_string())), 2));
        assert_eq!(remain.as_str(), ",def,ghi");

        // test error cases
        let mut remain = ",def".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Cannot parse empty, please use '' wrapped empty string.".to_string(),
                data: ",def".to_string(),
                at: 0,
                range: 1,
            })
        );

        let mut remain = "".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Cannot parse empty, please use '' wrapped empty string.".to_string(),
                data: "".to_string(),
                at: 0,
                range: 1,
            })
        );

        let mut remain = "a'bc,def,ghi".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Please wrap ''' character in single quotes".to_string(),
                data: "a'bc,def,ghi".to_string(),
                at: 1,
                range: 1,
            })
        );

        let mut remain = "a\"bc,def,ghi".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Please wrap '\"' character in single quotes".to_string(),
                data: "a\"bc,def,ghi".to_string(),
                at: 1,
                range: 1,
            })
        );

        let mut remain = "a bc,def,ghi".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Please wrap whitespace character in single quotes".to_string(),
                data: "a bc,def,ghi".to_string(),
                at: 1,
                range: 1,
            })
        );

        let mut remain = "a\tbc,def,ghi".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Please wrap whitespace character in single quotes".to_string(),
                data: "a\tbc,def,ghi".to_string(),
                at: 1,
                range: 1,
            })
        );

        let mut remain = "a\nbc,def,ghi".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Please wrap whitespace character in single quotes".to_string(),
                data: "a\nbc,def,ghi".to_string(),
                at: 1,
                range: 1,
            })
        );

        let mut remain = "a\rbc,def,ghi".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Please wrap whitespace character in single quotes".to_string(),
                data: "a\rbc,def,ghi".to_string(),
                at: 1,
                range: 1,
            })
        );
    }

    #[test]
    fn test_parse_dict() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "{a:b,c:d},{a:b,c:d}".chars();
        let result = parser.parse_dict(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Object(
                    json!({"a": "b", "c": "d"}).as_object().unwrap().to_owned()
                )),
                9
            )
        );
        assert_eq!(remain.as_str(), ",{a:b,c:d}");

        let mut remain = "{a:{a:b,c:d},b:{a:b,c:d},c:[1,2,3],d:false,e:null,f:'1'}".chars();
        let result = parser.parse_dict(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Object(json!({"a": {"a": "b", "c": "d"}, "b": {"a": "b", "c": "d"}, "c": [1, 2, 3], "d": false, "e": null, "f": "1"}).as_object().unwrap().to_owned())),
                56
            )
        );
        assert_eq!(remain.as_str(), "");

        let mut remain = "{},abc".chars();
        let result = parser.parse_dict(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Object(json!({}).as_object().unwrap().to_owned())),
                2
            )
        );
        assert_eq!(remain.as_str(), ",abc");

        // test error cases
        let mut remain = "{a:b,c:d".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ',' or '}'".to_string(),
                data: "{a:b,c:d".to_string(),
                at: 8,
                range: 1,
            })
        );

        let mut remain = "{a:b,c:d,".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character '}'".to_string(),
                data: "{a:b,c:d,".to_string(),
                at: 9,
                range: 1,
            })
        );

        let mut remain = "{a:b,a:d}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Duplicate defined dict key 'a'".to_string(),
                data: "{a:b,a:d}".to_string(),
                at: 5,
                range: 1,
            })
        );

        let mut remain = "{'a\"':b,c:d}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Dict key should not contain double quotes".to_string(),
                data: "{'a\"':b,c:d}".to_string(),
                at: 1,
                range: 4,
            })
        );

        let mut remain = "{'a'/':b,c:d}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Dict key should not contain single quotes".to_string(),
                data: "{'a'/':b,c:d}".to_string(),
                at: 1,
                range: 5,
            })
        );

        let mut remain = "{'a'bc:b,c:d}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ':'".to_string(),
                data: "{'a'bc:b,c:d}".to_string(),
                at: 4,
                range: 1,
            })
        );

        let mut remain = "{'a',c:d}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ':'".to_string(),
                data: "{'a',c:d}".to_string(),
                at: 4,
                range: 1,
            })
        );

        let mut remain = "{'a':".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Cannot parse empty".to_string(),
                data: "{'a':".to_string(),
                at: 5,
                range: 1,
            })
        );

        let mut remain = "{'a':{b:c,d:}}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Cannot parse empty, please use '' wrapped empty string.".to_string(),
                data: "{'a':{b:c,d:}}".to_string(),
                at: 12,
                range: 1,
            })
        );
        let mut remain = "{'a':{b:c,d:c]}".chars();
        let r = parser.parse_dict(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ',' or '}'".to_string(),
                data: "{'a':{b:c,d:c]}".to_string(),
                at: 13,
                range: 1,
            })
        );
    }

    #[test]
    fn test_parse_list() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "[a,b,c]".chars();
        let result = parser.parse_list(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Array(
                    json!(["a", "b", "c"]).as_array().unwrap().to_owned()
                )),
                7
            )
        );
        assert_eq!(remain.as_str(), "");

        let mut remain = "[1,2,3],bcd".chars();
        let result = parser.parse_list(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Array(
                    json!([1, 2, 3]).as_array().unwrap().to_owned()
                )),
                7
            )
        );
        assert_eq!(remain.as_str(), ",bcd");

        let mut remain = "[[1,2,3],[4,5,6],[7,8,9]]".chars();
        let result = parser.parse_list(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                SHResult::Value(Value::Array(
                    json!([[1, 2, 3], [4, 5, 6], [7, 8, 9]])
                        .as_array()
                        .unwrap()
                        .to_owned()
                )),
                25
            )
        );
        assert_eq!(remain.as_str(), "");

        // test error cases
        let mut remain = "[1,2,3".chars();
        let r = parser.parse_list(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ',' or ']'".to_string(),
                data: "[1,2,3".to_string(),
                at: 6,
                range: 1,
            })
        );

        let mut remain = "[1,2,3,".chars();
        let r = parser.parse_list(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ']'".to_string(),
                data: "[1,2,3,".to_string(),
                at: 7,
                range: 1,
            })
        );

        let mut remain = "[1,2,[3,4}]".chars();
        let r = parser.parse_list(&mut remain);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ',' or ']'".to_string(),
                data: "[1,2,[3,4}]".to_string(),
                at: 9,
                range: 1,
            })
        );
    }

    // TODO: add test for help message parse
    // #[test]
    // fn test_shorthand_syntax_help_parse() {
    // }

    #[test]
    fn test_split_partial_value_key() {
        let parser = ShorthandSyntaxParser::default();

        // test blank value
        let value = "a=";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a", None)));

        let value = "a=''";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a", Some("''"))));

        let value = "'a'/=b'[1].b=";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a'/=b'[1].b", None)));

        // value with [+]
        let value = "a[+]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a[+]", Some("{c:d}"))));

        let value = "[+].b[+]='{c:d}'";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[+].b[+]", Some("'{c:d}'"))));

        let value = "[+][+].'a[+]'[+]='{c:d}'";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[+][+].'a[+]'[+]", Some("'{c:d}'"))));

        let value = "a={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a", Some("{c:d}"))));

        let value = "a.b='{c:d}'";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a.b", Some("'{c:d}'"))));

        let value = "a[1]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a[1]", Some("{c:d}"))));

        let value = "a[1].b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a[1].b", Some("{c:d}"))));

        let value = "a[1][2]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a[1][2]", Some("{c:d}"))));

        let value = "'a b'={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a b'", Some("{c:d}"))));

        let value = "'a '/'={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a '/'", Some("{c:d}"))));

        let value = "'a '/'.b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a '/'.b", Some("{c:d}"))));

        let value = "'a '/'[1]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a '/'[1]", Some("{c:d}"))));

        let value = "'a //'[1]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a //'[1]", Some("{c:d}"))));

        let value = "'a=b'[1].b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a=b'[1].b", Some("{c:d}"))));

        let value = "'a'/=b'[1].b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("'a'/=b'[1].b", Some("{c:d}"))));

        let value = "a.b[1].d={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a.b[1].d", Some("{c:d}"))));

        let value = "a.b[-1].d={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a.b[-1].d", Some("{c:d}"))));

        let value = "a.b[1][2].d={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a.b[1][2].d", Some("{c:d}"))));

        let value = "a.'b cd'.d={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a.'b cd'.d", Some("{c:d}"))));

        let value = "a.'b c'/d'.d={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a.'b c'/d'.d", Some("{c:d}"))));

        let value = "[1]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1]", Some("{c:d}"))));

        let value = "[1].b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1].b", Some("{c:d}"))));

        let value = "[1][2]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1][2]", Some("{c:d}"))));

        let value = "[1][2].b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1][2].b", Some("{c:d}"))));

        // test invalid cases which will not blocked by regex, but will raise error when parse partial key parts
        let value = "a[-1]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a[-1]", Some("{c:d}"))));

        let value = "a[]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("a[]", Some("{c:d}"))));

        let value = "[1]..b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1]..b", Some("{c:d}"))));

        let value = "[1].b.={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1].b.", Some("{c:d}"))));

        let value = "[1].[2]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, Some(("[1].[2]", Some("{c:d}"))));

        // test invalid cases which should be blocked by regex
        let value = "[a]={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, None);

        let value = "a.'b cd'e={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, None);

        let value = "a[1]b={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, None);

        let value = ".a={c:d}";
        let result = parser.split_partial_value(&value);
        assert_eq!(result, None);
    }

    #[test]
    fn test_parse_partial_value_key_parts() {
        let parser = ShorthandSyntaxParser::default();
        let key = "a";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![SHPartialKeyPart::parse_name("a").unwrap()]
            })
        );

        let key = "a.b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "a[1]";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap()
                ]
            })
        );

        let key = "a[+1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::AppendIndex(1),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let value = "[+2].b[+0]";
        let result = parser.parse_partial_value_key_parts(&value);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::AppendIndex(2), // append
                    SHPartialKeyPart::parse_name("b").unwrap(),
                    SHPartialKeyPart::AppendIndex(0) // append
                ]
            })
        );

        let value = "[+1][+2].'a[+1]'[+2]";
        let result = parser.parse_partial_value_key_parts(&value);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_index("+1").unwrap(),
                    SHPartialKeyPart::parse_index("+2").unwrap(),
                    SHPartialKeyPart::parse_name("a[+1]").unwrap(),
                    SHPartialKeyPart::parse_index("+2").unwrap()
                ]
            })
        );

        let key = "a[1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "a[1][2]";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_index("2").unwrap()
                ]
            })
        );

        let key = "'a b'";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![SHPartialKeyPart::parse_name("a b").unwrap()]
            })
        );

        let key = "'a '/'";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![SHPartialKeyPart::parse_name("a '").unwrap()]
            })
        );

        let key = "'a '/'.b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a '").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "'a '/'[1]";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a '").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap()
                ]
            })
        );

        let key = "'a //'[1]";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a //").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap()
                ]
            })
        );

        let key = "'a=b'[1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a=b").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "'a'/=b'[1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a'=b").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "a.b[1].d";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_name("d").unwrap()
                ]
            })
        );

        let key = "a[+1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::AppendIndex(1),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "a.b[1][2].d";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap(),
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_index("2").unwrap(),
                    SHPartialKeyPart::parse_name("d").unwrap()
                ]
            })
        );

        let key = "a.'b cd'.d";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_name("b cd").unwrap(),
                    SHPartialKeyPart::parse_name("d").unwrap()
                ]
            })
        );

        let key = "a.'b c'/d'.d";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("a").unwrap(),
                    SHPartialKeyPart::parse_name("b c'd").unwrap(),
                    SHPartialKeyPart::parse_name("d").unwrap()
                ]
            })
        );

        let key = "[1]";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![SHPartialKeyPart::parse_index("1").unwrap()]
            })
        );

        let key = "[1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "[1][2]";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_index("2").unwrap()
                ]
            })
        );

        let key = "[1][2].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_index("1").unwrap(),
                    SHPartialKeyPart::parse_index("2").unwrap(),
                    SHPartialKeyPart::parse_name("b").unwrap()
                ]
            })
        );

        let key = "1.2.3";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Ok(SHPartialKeys {
                parts: vec![
                    SHPartialKeyPart::parse_name("1").unwrap(),
                    SHPartialKeyPart::parse_name("2").unwrap(),
                    SHPartialKeyPart::parse_name("3").unwrap()
                ]
            })
        );
    }

    #[test]
    fn test_parse_partial_value_key_parts_error() {
        let parser = ShorthandSyntaxParser::default();
        let key = "a[1.b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ']'".to_string(),
                data: key.to_string(),
                at: 3,
                range: 1,
            })
        );

        let key = "a.b[1.b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Expect character ']'".to_string(),
                data: key.to_string(),
                at: 5,
                range: 1,
            })
        );

        let key = "a.b[-1].d";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Invalid index, support integer for direct index or integer with + for append index.".to_string(),
                data: key.to_string(),
                at: 3,
                range: 4,
            })
        );

        let key = "a[-1].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Invalid index, support integer for direct index or integer with + for append index.".to_string(),
                data: key.to_string(),
                at: 1,
                range: 4,
            })
        );

        let key = "a[].b";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: key.to_string(),
                at: 1,
                range: 2,
            })
        );

        let key = "a.b[].c";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: key.to_string(),
                at: 3,
                range: 2,
            })
        );

        let key = "a.''.c";
        let result = parser.parse_partial_value_key_parts(&key);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: key.to_string(),
                at: 1,
                range: 3,
            })
        );

        let value = "[1]..b";
        let result = parser.parse_partial_value_key_parts(&value);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: value.to_string(),
                at: 3,
                range: 1,
            })
        );

        let value = "[1].b.";
        let result = parser.parse_partial_value_key_parts(&value);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: value.to_string(),
                at: 5,
                range: 1,
            })
        );

        let value = "[1].[2]";
        let result = parser.parse_partial_value_key_parts(&value);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Miss prop name".to_string(),
                data: value.to_string(),
                at: 3,
                range: 1,
            })
        );

        let value = "b[a]";
        let result = parser.parse_partial_value_key_parts(&value);
        assert_eq!(
            result,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Invalid index, support integer for direct index or integer with + for append index.".to_string(),
                data: value.to_string(),
                at: 1,
                range: 3,
            })
        );
    }
}
