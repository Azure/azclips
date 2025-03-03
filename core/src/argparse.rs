use std::collections::HashMap;
use std::fmt::format;
use std::str::Chars;
use std::sync::LazyLock;

use regex::Regex;
use serde_json::{json, Map, Value};

use crate::error::{AzInternalError, AzResult, AzUserUsageError};
use crate::pb::command::{self};

pub enum ArgOp {
    PromptInput(ArgPromptInputOp),
    ValueAssign(ArgValueAssignOp),
}

pub struct ArgPromptInputOp {
    keys: Vec<String>,
    prompt: crate::pb::command::CmdArgPromptInput,
}
pub struct ArgValueAssignOp {
    keys: Vec<String>,
    /// whether the value is filled from the blank value or not
    from_blank: bool,
    value: Value,
}

/// convert the values into the argument operations, return None when the argument is not found
pub fn parse_cmd_arg(
    cmd: &command::CmdCommand,
    flag: &String,
    values: &[String],
) -> AzResult<Option<Vec<ArgOp>>> {
    let mut matched_arg = None;
    for arg in cmd.args.iter() {
        if arg.options.contains(flag) {
            matched_arg = Some(arg);
            break;
        }
    }

    if matched_arg.is_none() {
        return Ok(None);
    }

    let arg = matched_arg.unwrap();

    let ops = parse_arg_values(cmd, arg, values, &[])?;

    Ok(Some(ops))
}

fn parse_arg_values(
    cmd: &command::CmdCommand,
    arg: &command::CmdArg,
    values: &[String],
    prefix_keys: &[String],
) -> AzResult<Vec<ArgOp>> {
    let arg_type = arg.arg_type_unwrap_cls_ok(cmd)?;

    if values.is_empty() {
        // allow prompt input
        if let Some(prompt) = arg.prompt.as_ref() {
            return Ok(vec![ArgOp::PromptInput(ArgPromptInputOp {
                keys: prefix_keys.to_vec(),
                prompt: prompt.to_owned(),
            })]);
        }

        // else use blank value instead
        let blank = arg
            .blank
            .as_ref()
            .ok_or(AzUserUsageError::InvalidArgumentValueError(format!(
                "Argument '{}' requires a value but no value is provided",
                prefix_keys.join(".")
            )))?
            .to_owned();
        let value: Value = blank.try_into()?;

        if value.is_null() && !arg.nullable {
            return Err(AzInternalError::InvalidArgumentDefinitionError(format!(
                "Argument '{}' is not nullable, but the blank value is null",
                prefix_keys.join(".")
            ))
            .into());
        }

        return Ok(vec![ArgOp::ValueAssign(ArgValueAssignOp {
            keys: prefix_keys.to_vec(),
            from_blank: true,
            value,
        })]);
    }

    if values.len() > 1 {
        match arg_type {
            command::cmd_arg::ArgType::Str(_)
            | command::cmd_arg::ArgType::Number(_)
            | command::cmd_arg::ArgType::Boolean(_) => {
                // The simple type argument can't accept multiple values
                return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' can't accept multiple values",
                    prefix_keys.join(".")
                ))
                .into());
            }
            _ => {}
        }
    }

    let ops = values
        .iter()
        .map(|value| parse_arg_value(cmd, arg, value, &prefix_keys))
        .collect::<Result<Vec<_>, _>>()?;

    Ok(ops)
}

fn parse_arg_value(
    cmd: &command::CmdCommand,
    arg: &command::CmdArg,
    value: &String,
    prefix_keys: &[String],
) -> AzResult<ArgOp> {
    let arg_type = arg.arg_type_unwrap_cls_ok(cmd)?;
    match arg_type {
        command::cmd_arg::ArgType::Str(_)
        | command::cmd_arg::ArgType::Number(_)
        | command::cmd_arg::ArgType::Boolean(_) => {
            // let v = SHORTHAND_SYNTAX_PARSER.parse(value, true)?;
            unimplemented!()
        }
        command::cmd_arg::ArgType::Object(_) => {}
        command::cmd_arg::ArgType::Array(_) => {}
        command::cmd_arg::ArgType::AnyType(_) => {}
        command::cmd_arg::ArgType::Cls(_) => {
            unreachable!("cls type should not be parsed here");
        }
    }

    unimplemented!()
}

struct ShorthandSyntaxParser {
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
    item_separator: char,
    partial_value_key_pattern: Regex,
}

static SHORTHAND_SYNTAX_PARSER: LazyLock<ShorthandSyntaxParser> =
    LazyLock::new(ShorthandSyntaxParser::default);

const PARTIAL_VALUE_KEY_PATTERN: &'static str = r"^(((\[-?[0-9]+])|((([a-zA-Z0-9_\-]+)|('([^']*)'(/([^']*)')*))(\[-?[0-9]+])?))(\.(([a-zA-Z0-9_\-]+)|('([^']*)'(/([^']*)')*))(\[-?[0-9]+])?)*)=(.*)$";

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
            item_separator: ',',
            partial_value_key_pattern: Regex::new(PARTIAL_VALUE_KEY_PATTERN).unwrap(),
        }
    }
}

#[derive(Debug, PartialEq)]
enum ShorthandSyntaxResult {
    /// contains the index keys for the help message
    Help(Vec<String>),
    Value(Value),
}

impl ShorthandSyntaxParser {
    fn parse(
        &self,
        value: &str,
        is_sample: bool,
    ) -> Result<ShorthandSyntaxResult, AzUserUsageError> {
        if is_sample {
            if *value == *self.null_expr {
                Ok(ShorthandSyntaxResult::Value(Value::Null))
            } else if *value == *self.help_expr {
                Ok(ShorthandSyntaxResult::Help(vec![]))
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
                    Ok(result)
                }
            } else {
                // return the bool, number, or string value directly
                if let Ok(v) = serde_json::from_str::<Value>(value) {
                    match v {
                        Value::Bool(v) => Ok(ShorthandSyntaxResult::Value(Value::Bool(v))),
                        Value::Number(v) => Ok(ShorthandSyntaxResult::Value(Value::Number(v))),
                        _ => Ok(ShorthandSyntaxResult::Value(Value::String(
                            value.to_string(),
                        ))),
                    }
                } else {
                    Ok(ShorthandSyntaxResult::Value(Value::String(
                        value.to_string(),
                    )))
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
    ) -> Result<(ShorthandSyntaxResult, usize), AzUserUsageError> {
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
                    return Ok((ShorthandSyntaxResult::Value(Value::String(result)), idx));
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

    fn parse_value(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(ShorthandSyntaxResult, usize), AzUserUsageError> {
        let str = remain.as_str();
        if str.starts_with(self.dict_start_char) {
            self.parse_dict(remain)
        } else if str.starts_with(self.list_start_char) {
            self.parse_list(remain)
        } else {
            self.parse_string(remain, true)
        }
    }

    fn parse_dict(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(ShorthandSyntaxResult, usize), AzUserUsageError> {
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
                return Ok((ShorthandSyntaxResult::Value(Value::Object(result)), idx));
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
                ShorthandSyntaxResult::Value(value) => value.as_str().unwrap().to_string(),
                ShorthandSyntaxResult::Help(keys) => {
                    return Ok((ShorthandSyntaxResult::Help(keys), idx));
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
                ShorthandSyntaxResult::Help(mut keys) => {
                    keys.push(k);
                    return Ok((ShorthandSyntaxResult::Help(keys), idx));
                }
                ShorthandSyntaxResult::Value(value) => {
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

    fn parse_list(
        &self,
        remain: &mut Chars<'_>,
    ) -> Result<(ShorthandSyntaxResult, usize), AzUserUsageError> {
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
                return Ok((ShorthandSyntaxResult::Value(Value::Array(result)), idx));
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
                ShorthandSyntaxResult::Help(mut keys) => {
                    keys.push("0".to_string());
                    return Ok((ShorthandSyntaxResult::Help(keys), idx));
                }
                ShorthandSyntaxResult::Value(value) => {
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
    ) -> Result<(ShorthandSyntaxResult, usize), AzUserUsageError> {
        let data = remain.as_str();
        let mut str = remain.as_str();
        if str.starts_with(self.single_quote_char) {
            return self.parse_single_quotes_string(remain);
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
                msg: "Cannot parse empty, for empty string, please use '' instead".to_string(),
                data: data.to_string(),
                at: idx,
                range: 1,
            });
        }

        if result == self.null_expr {
            Ok((ShorthandSyntaxResult::Value(Value::Null), idx))
        } else if result == self.help_expr {
            Ok((ShorthandSyntaxResult::Help(vec![]), idx))
        } else if convert_simple_type {
            match serde_json::from_str::<Value>(&result) {
                Ok(Value::Bool(v)) => Ok((ShorthandSyntaxResult::Value(Value::Bool(v)), idx)),
                Ok(Value::Number(v)) => Ok((ShorthandSyntaxResult::Value(Value::Number(v)), idx)),
                _ => Ok((ShorthandSyntaxResult::Value(Value::String(result)), idx)),
            }
        } else {
            Ok((ShorthandSyntaxResult::Value(Value::String(result)), idx))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_simple_value_null() {
        let parser = ShorthandSyntaxParser::default();

        let result = parser.parse("null", true).unwrap();
        assert_eq!(result, ShorthandSyntaxResult::Value(Value::Null));

        let result = parser.parse("Null", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("Null".to_string()))
        );

        let result = parser.parse("NULL", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("NULL".to_string()))
        );

        let result = parser.parse("'null'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("null".to_string()))
        );

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
        assert_eq!(result, ShorthandSyntaxResult::Value(Value::Bool(true)));
        let result = parser.parse("false", true).unwrap();
        assert_eq!(result, ShorthandSyntaxResult::Value(Value::Bool(false)));
        let result = parser.parse("True", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("True".to_string()))
        );
        let result = parser.parse("False", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("False".to_string()))
        );
        let result = parser.parse("'true'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("true".to_string()))
        );
        let result = parser.parse("'false'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("false".to_string()))
        );

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
            ShorthandSyntaxResult::Value(Value::Number(serde_json::Number::from(0)))
        );
        let result = parser.parse("0.0", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Number(serde_json::Number::from_f64(0.0).unwrap()))
        );

        let result = parser.parse("123", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Number(serde_json::Number::from(123)))
        );
        let result = parser.parse("-123", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Number(serde_json::Number::from(-123)))
        );
        let result = parser.parse("123.456", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Number(
                serde_json::Number::from_f64(123.456).unwrap()
            ))
        );
        let result = parser.parse("-123.456", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Number(
                serde_json::Number::from_f64(-123.456).unwrap()
            ))
        );

        let result = parser.parse("'0'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("0".to_string()))
        );

        let result = parser.parse("'0.0'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("0.0".to_string()))
        );

        let result = parser.parse("'123'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("123".to_string()))
        );
        let result = parser.parse("'-123'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("-123".to_string()))
        );

        let result = parser.parse("'123.456'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("123.456".to_string()))
        );
        let result = parser.parse("'-123.456'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("-123.456".to_string()))
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
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("hello".to_string()))
        );

        let result = parser.parse("'hello world'", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("hello world".to_string()))
        );

        let result = parser.parse("hello world", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("hello world".to_string()))
        );

        let result = parser.parse("{a:b}", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("{a:b}".to_string()))
        );

        let result = parser.parse("{\"a\":\"b\"}", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("{\"a\":\"b\"}".to_string()))
        );

        let result = parser.parse("[1, 2, 3]", true).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("[1, 2, 3]".to_string()))
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
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("hello world".to_string())),
                13
            )
        );

        assert_eq!(remain.as_str(), "");

        let mut remain = "'hello world'abc".chars();
        let result = parser.parse_single_quotes_string(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("hello world".to_string())),
                13
            )
        );

        assert_eq!(remain.as_str(), "abc");

        let mut remain = "'hello world'/ab/c//'".chars();
        let result = parser.parse_single_quotes_string(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("hello world'ab/c//".to_string())),
                21
            )
        );
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
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::String("abc".to_string()))
        );

        let result = parser.parse("123", false).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Number(serde_json::Number::from(123)))
        );

        let result = parser.parse("true", false).unwrap();
        assert_eq!(result, ShorthandSyntaxResult::Value(Value::Bool(true)));

        let result = parser.parse("false", false).unwrap();
        assert_eq!(result, ShorthandSyntaxResult::Value(Value::Bool(false)));

        let result = parser.parse("null", false).unwrap();
        assert_eq!(result, ShorthandSyntaxResult::Value(Value::Null));

        let result = parser.parse("{a:b}", false).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Object(
                json!({"a": "b"}).as_object().unwrap().clone()
            ))
        );

        let result = parser.parse("[1,2,3]", false).unwrap();
        assert_eq!(
            result,
            ShorthandSyntaxResult::Value(Value::Array(
                json!([1, 2, 3]).as_array().unwrap().clone()
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
            ShorthandSyntaxResult::Value(Value::Object(
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
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                3
            )
        );

        let mut remain = "'abc'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                5
            )
        );

        let mut remain = "123".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::Number(serde_json::Number::from(123))),
                3
            )
        );

        let mut remain = "'123'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("123".to_string())),
                5
            )
        );

        let mut remain = "true".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(result, (ShorthandSyntaxResult::Value(Value::Bool(true)), 4));

        let mut remain = "'true'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("true".to_string())),
                6
            )
        );

        let mut remain = "false".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (ShorthandSyntaxResult::Value(Value::Bool(false)), 5)
        );

        let mut remain = "'false'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("false".to_string())),
                7
            )
        );

        let mut remain = "null".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(result, (ShorthandSyntaxResult::Value(Value::Null), 4));

        let mut remain = "'null'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("null".to_string())),
                6
            )
        );

        let mut remain = "'{\"a\":\"b\"}'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("{\"a\":\"b\"}".to_string())),
                11
            )
        );

        let mut remain = "'[1,2,3]'".chars();
        let result = parser.parse_string(&mut remain, true).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("[1,2,3]".to_string())),
                9
            )
        );
    }

    #[test]
    fn test_parse_string_not_convert_simple_type() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "abc".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                3
            )
        );

        let mut remain = "'abc'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                5
            )
        );

        let mut remain = "123".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("123".to_string())),
                3
            )
        );

        let mut remain = "'123'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("123".to_string())),
                5
            )
        );

        let mut remain = "true".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("true".to_string())),
                4
            )
        );

        let mut remain = "'true'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("true".to_string())),
                6
            )
        );

        let mut remain = "false".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("false".to_string())),
                5
            )
        );

        let mut remain = "'false'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("false".to_string())),
                7
            )
        );

        let mut remain = "null".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(result, (ShorthandSyntaxResult::Value(Value::Null), 4));

        let mut remain = "'null'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("null".to_string())),
                6
            )
        );

        let mut remain = "'{\"a\":\"b\"}'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("{\"a\":\"b\"}".to_string())),
                11
            )
        );

        let mut remain = "'[1,2,3]'".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("[1,2,3]".to_string())),
                9
            )
        );
    }

    #[test]
    fn test_parse_string_part() {
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "abc,def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                3
            )
        );
        assert_eq!(remain.as_str(), ",def");
        let parser = ShorthandSyntaxParser::default();
        let mut remain = "'a b c',def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("a b c".to_string())),
                7
            )
        );
        assert_eq!(remain.as_str(), ",def");

        let mut remain = "abc:def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                3
            )
        );
        assert_eq!(remain.as_str(), ":def");

        let mut remain = "abc],def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                3
            )
        );
        assert_eq!(remain.as_str(), "],def");

        let mut remain = "abc},def".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("abc".to_string())),
                3
            )
        );
        assert_eq!(remain.as_str(), "},def");

        let mut remain = "'',def,ghi".chars();
        let result = parser.parse_string(&mut remain, false).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::String("".to_string())),
                2
            )
        );
        assert_eq!(remain.as_str(), ",def,ghi");

        // test error cases
        let mut remain = ",def".chars();
        let r = parser.parse_string(&mut remain, false);
        assert_eq!(
            r,
            Err(AzUserUsageError::InvalidShorthandSyntaxError {
                msg: "Cannot parse empty".to_string(),
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
                msg: "Cannot parse empty".to_string(),
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
                ShorthandSyntaxResult::Value(Value::Object(
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
                ShorthandSyntaxResult::Value(Value::Object(json!({"a": {"a": "b", "c": "d"}, "b": {"a": "b", "c": "d"}, "c": [1, 2, 3], "d": false, "e": null, "f": "1"}).as_object().unwrap().to_owned())),
                56
            )
        );
        assert_eq!(remain.as_str(), "");

        let mut remain = "{},abc".chars();
        let result = parser.parse_dict(&mut remain).unwrap();
        assert_eq!(
            result,
            (
                ShorthandSyntaxResult::Value(Value::Object(
                    json!({}).as_object().unwrap().to_owned()
                )),
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
                msg: "Cannot parse empty, for empty string, please use '' instead".to_string(),
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
                ShorthandSyntaxResult::Value(Value::Array(
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
                ShorthandSyntaxResult::Value(Value::Array(
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
                ShorthandSyntaxResult::Value(Value::Array(
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
}
