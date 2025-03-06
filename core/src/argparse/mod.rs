mod partial;
mod shorthand;

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
use crate::pb::{BOOLEAN_ARG_BLANK_DEFAULT, BOOLEAN_ARG_ENUM_DEFAULT};

use self::partial::{SHPartialKeyPart, SHPartialKeys};
use self::shorthand::{SHResult, SHORTHAND_SYNTAX_PARSER};

pub enum ArgOp {
    PromptInput(ArgPromptInputOp),
    ValueAssign(ArgValueAssignOp),
    Help(ArgHelpOp),
}

pub struct ArgPromptInputOp {
    keys: SHPartialKeys,
    prompt: crate::pb::command::CmdArgPromptInput,
}
pub struct ArgValueAssignOp {
    keys: SHPartialKeys,
    /// whether the value is filled from the blank value or not
    from_blank: bool,
    value: Value,
}

pub struct ArgHelpOp {
    keys: SHPartialKeys,
}

/// convert the values into the argument operations, return None when the argument is not found
pub fn parse_cmd_arg(
    cmd: &command::CmdCommand,
    flag: &str,
    values: &[String],
) -> AzResult<Option<Vec<ArgOp>>> {
    let mut matched_arg = None;
    for arg in cmd.args.iter() {
        if arg.options.iter().any(|o| o == flag) {
            matched_arg = Some(arg);
            break;
        }
    }

    if matched_arg.is_none() {
        return Ok(None);
    }

    let arg = matched_arg.unwrap();

    let ops = parse_arg_values(cmd, arg, values, &SHPartialKeys::default())?;

    Ok(Some(ops))
}

fn parse_arg_values(
    cmd: &command::CmdCommand,
    arg: &command::CmdArg,
    values: &[String],
    prefix_keys: &SHPartialKeys,
) -> AzResult<Vec<ArgOp>> {
    match arg.arg_type_unwrap_cls_ok(cmd)? {
        command::cmd_arg::ArgType::Str(_)
        | command::cmd_arg::ArgType::Number(_)
        | command::cmd_arg::ArgType::Boolean(_) => {
            if values.len() > 1 {
                // The simple type argument can't accept multiple values
                Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' can't accept multiple values",
                    prefix_keys
                ))
                .into())
            } else {
                let value = values.first().map(|v| v.as_str());
                let op = parse_arg_value_str(cmd, arg, value, &prefix_keys)?;
                Ok(vec![op])
            }
        }
        command::cmd_arg::ArgType::AnyType(_) => {
            if values.len() > 1 {
                // The simple type argument can't accept multiple values
                Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' can't accept multiple values",
                    prefix_keys
                ))
                .into())
            } else if values.len() == 0 {
                let op = parse_arg_value_str(cmd, arg, None, &prefix_keys)?;
                Ok(vec![op])
            } else {
                let value = values.first().unwrap();
                if let Some(_) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' is any type arg which only support full value shorthand syntax, please don't use partial value shorthand syntax. If it's a simple string, please wrap it with single quotes.",
                    prefix_keys,
                ))
                .into());
                }
                let op = parse_arg_value_str(cmd, arg, Some(value), &prefix_keys)?;
                Ok(vec![op])
            }
        }
        command::cmd_arg::ArgType::Object(_) => {
            if values.len() == 0 {
                let op = parse_arg_value_str(cmd, arg, None, &prefix_keys)?;
                Ok(vec![op])
            } else {
                let mut ops = Vec::new();
                for value in values {
                    if let Some((key, value)) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
                        let keys = SHORTHAND_SYNTAX_PARSER.parse_partial_value_key_parts(key)?;
                        let sub_arg = retrieve_internal_arg_by_keys(cmd, arg, &keys)?;
                        let sub_prefix_keys = prefix_keys.to_owned().with_extend(keys.into_iter());
                        let op = parse_arg_value_str(cmd, sub_arg, value, &sub_prefix_keys)?;
                        ops.push(op);
                    } else {
                        let op = parse_arg_value_str(cmd, arg, Some(value), &prefix_keys)?;
                        ops.push(op);
                    }
                }
                Ok(ops)
            }
        }
        command::cmd_arg::ArgType::Array(_) => {
            // here we dropped the support for space separated elements, which will cause the ambiguity
            if values.len() == 0 {
                let op = parse_arg_value_str(cmd, arg, None, &prefix_keys)?;
                Ok(vec![op])
            } else {
                let mut ops = Vec::new();
                for value in values {
                    if let Some((key, value)) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
                        let keys = SHORTHAND_SYNTAX_PARSER.parse_partial_value_key_parts(key)?;
                        let sub_arg = retrieve_internal_arg_by_keys(cmd, arg, &keys)?;
                        let sub_prefix_keys = prefix_keys.to_owned().with_extend(keys.into_iter());
                        let op = parse_arg_value_str(cmd, sub_arg, value, &sub_prefix_keys)?;
                        ops.push(op);
                    } else {
                        let op = parse_arg_value_str(cmd, arg, Some(value), &prefix_keys)?;
                        ops.push(op);
                    }
                }
                Ok(ops)
            }
        }
        command::cmd_arg::ArgType::Cls(_) => {
            unreachable!()
        }
    }
}

fn parse_arg_value_str(
    cmd: &command::CmdCommand,
    arg: &command::CmdArg,
    value: Option<&str>, // None for the blank input
    prefix_keys: &SHPartialKeys,
) -> AzResult<ArgOp> {
    let arg_type = arg.arg_type_unwrap_cls_ok(cmd)?;

    // handle the blank input
    if value.is_none() {
        // allow prompt input
        if let Some(prompt) = arg.prompt.as_ref() {
            return Ok(ArgOp::PromptInput(ArgPromptInputOp {
                keys: prefix_keys.to_owned(),
                prompt: prompt.to_owned(),
            }));
        }

        // else use blank value instead
        let blank = arg
            .blank
            .as_ref()
            .or_else(|| match arg_type {
                // apply default blank value for boolean type
                ArgType::Boolean(_) => Some(&BOOLEAN_ARG_BLANK_DEFAULT),
                _ => None,
            })
            .ok_or(AzUserUsageError::InvalidArgumentValueError(format!(
                "Argument '{}' requires a value but no value is provided",
                prefix_keys
            )))?
            .to_owned();
        let value: Value = blank.try_into()?;

        if value.is_null() && !arg.nullable {
            return Err(AzInternalError::InvalidArgumentDefinitionError(format!(
                "Argument '{}' is not nullable, but the blank value is null",
                prefix_keys
            ))
            .into());
        }

        return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
            keys: prefix_keys.to_owned(),
            from_blank: true,
            value,
        }));
    }

    let value = value.unwrap();

    match arg_type {
        command::cmd_arg::ArgType::Str(str_type) => {
            match SHORTHAND_SYNTAX_PARSER.parse(value, true)? {
                SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp {
                    keys: prefix_keys.to_owned().with_extend(keys.into_iter().rev()),
                })),
                SHResult::Value(mut value) => {
                    if value.is_null() {
                        if !arg.nullable {
                            return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Argument '{}' value cannot be null",
                                prefix_keys
                            ))
                            .into());
                        } else {
                            return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                                keys: prefix_keys.to_owned(),
                                from_blank: false,
                                value,
                            }));
                        }
                    }

                    // convert to string value
                    match &value {
                        Value::Number(n) => {
                            value = Value::String(n.to_string());
                        }
                        Value::Bool(b) => {
                            value = Value::String(b.to_string());
                        }
                        Value::String(_) => {} // ignore
                        _ => {
                            unreachable!()
                        }
                    }

                    // handle the enum type
                    if let Some(e) = str_type.r#enum.as_ref() {
                        if let Some(v) = e.find_value_by_name(value.as_str().unwrap())? {
                            value = v
                        }
                        value = e.validate_value(value)?
                    }

                    Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                        keys: prefix_keys.to_owned(),
                        from_blank: false,
                        value,
                    }))
                }
            }
        }
        command::cmd_arg::ArgType::Number(num_type) => {
            match SHORTHAND_SYNTAX_PARSER.parse(value, true)? {
                SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp {
                    keys: prefix_keys.to_owned().with_extend(keys.into_iter().rev()),
                })),
                SHResult::Value(mut value) => {
                    if value.is_null() {
                        if !arg.nullable {
                            return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Argument '{}' value cannot be null",
                                prefix_keys
                            ))
                            .into());
                        } else {
                            return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                                keys: prefix_keys.to_owned(),
                                from_blank: false,
                                value,
                            }));
                        }
                    }
                    if let Some(e) = num_type.r#enum.as_ref() {
                        match &value {
                            Value::String(v) => {
                                if let Some(v) = e.find_value_by_name(v)? {
                                    value = v
                                }
                            }
                            _ => {}
                        }
                        value = e.validate_value(value)?
                    }

                    match &value {
                        // the number will be formatted to detailed types based on the format in the format stage,
                        // so here no need to make type transformation
                        Value::Number(_) => Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                            keys: prefix_keys.to_owned(),
                            from_blank: false,
                            value,
                        })),
                        Value::Bool(v) => {
                            Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Argument '{}' expected a number value, but got a boolean value '{}'",
                                prefix_keys,
                                v
                            ))
                            .into())
                        }
                        Value::String(v) => {
                            Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Argument '{}' expected a number value, but got a string value '{}'",
                                prefix_keys,
                                v
                            ))
                            .into())
                        }
                        _ => {
                            unreachable!()
                        }
                    }
                }
            }
        }
        command::cmd_arg::ArgType::Boolean(_) => {
            match SHORTHAND_SYNTAX_PARSER.parse(value, true)? {
                SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp {
                    keys: prefix_keys.to_owned().with_extend(keys.into_iter().rev()),
                })),
                SHResult::Value(mut value) => {
                    if value.is_null() {
                        if !arg.nullable {
                            return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Argument '{}' value cannot be null",
                                prefix_keys,
                            ))
                            .into());
                        } else {
                            return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                                keys: prefix_keys.to_owned(),
                                from_blank: false,
                                value,
                            }));
                        }
                    }

                    match &value {
                        Value::Bool(_) => Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                            keys: prefix_keys.to_owned(),
                            from_blank: false,
                            value,
                        })),
                        Value::String(v) => {
                            if let Some(v) = BOOLEAN_ARG_ENUM_DEFAULT.find_value_by_name(v)? {
                                value = v;
                                Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                                    keys: prefix_keys.to_owned(),
                                    from_blank: false,
                                    value,
                                }))
                            } else {
                                Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                    "Argument '{}' expected a boolean value, but got a string value '{}'",
                                    prefix_keys,
                                    v
                                ))
                                .into())
                            }
                        }
                        Value::Number(v) => {
                            Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Argument '{}' expected a boolean value, but got a number value '{}'",
                                prefix_keys,
                                v
                            ))
                            .into())
                        }
                        _ => {
                            unreachable!()
                        }
                    }
                }
            }
        }
        command::cmd_arg::ArgType::AnyType(_) => {
            // // Try to parse the value from the file first when the value is prefixed with the file prefix
            // if let Some(value) = value
            //     .strip_prefix(FILE_PREFIX)
            //     .map(read_serde_value_from_file)
            //     .transpose()?
            //     .unwrap_or(None)
            // {
            //     Ok(ArgOp::ValueAssign(ArgValueAssignOp {
            //         keys: prefix_keys.to_owned(),
            //         from_blank: false,
            //         value,
            //     }))
            // } else
            if let Ok(value) = SHORTHAND_SYNTAX_PARSER.parse(value, false) {
                // Try parse with the shorthand syntax parser then
                match value {
                    SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp {
                        keys: prefix_keys.to_owned().with_extend(keys.into_iter().rev()),
                    })),
                    SHResult::Value(value) => Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                        keys: prefix_keys.to_owned(),
                        from_blank: false,
                        value,
                    })),
                }
            } else if let Ok(value) = serde_json::from_str::<Value>(value) {
                // If the shorthand syntax parser failed, try to parse with the serde_json
                Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                    keys: prefix_keys.to_owned(),
                    from_blank: false,
                    value,
                }))
            } else {
                // If the serde_json also failed, assume it's a simple string value
                Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                    keys: prefix_keys.to_owned(),
                    from_blank: false,
                    value: Value::String(value.to_string()),
                }))
            }
        }
        command::cmd_arg::ArgType::Object(obj_type) => {
            // let mut serde_value = value
            //     .strip_prefix(FILE_PREFIX)
            //     .map(read_serde_value_from_file)
            //     .transpose()?
            //     .unwrap_or(None);

            // if serde_value.is_none() {

            // }

            let mut serde_value = None;

            if let Ok(value) = SHORTHAND_SYNTAX_PARSER.parse(value, false) {
                match value {
                    SHResult::Help(keys) => {
                        return Ok(ArgOp::Help(ArgHelpOp {
                            keys: prefix_keys.to_owned().with_extend(keys.into_iter().rev()),
                        }))
                    }
                    SHResult::Value(value) => {
                        serde_value = Some(value);
                    }
                }
                // TODO: expand internal files
            } else if let Ok(value) = serde_json::from_str::<Value>(value) {
                serde_value = Some(value);
            }

            // if let Some(v) = value
            //     .strip_prefix(FILE_PREFIX)
            //     .and_then(|v| Some(read_serde_value_from_file(v)))
            //     .unwrap_or(Ok(None))?
            // {
            //     // if let Some(value) = read_serde_value_from_file(file_path)? {
            //     //     unimplemented!()
            //     // }
            // }
            unimplemented!()
        }
        command::cmd_arg::ArgType::Array(arr_type) => {
            // if let Some(file_path) = value.strip_prefix(FILE_PREFIX) {
            //     if let Some(value) = read_serde_value_from_file(file_path)? {
            //         unimplemented!()
            //     }
            // }

            unimplemented!()
        }
        command::cmd_arg::ArgType::Cls(_) => {
            unreachable!("cls type should not be parsed here");
        }
    }
}

fn format_arg_value() {}

fn retrieve_internal_arg_by_keys<'a>(
    cmd: &'a command::CmdCommand,
    arg: &'a command::CmdArg,
    keys: &'_ SHPartialKeys,
) -> AzResult<&'a command::CmdArg> {
    let mut arg = arg;
    for (i, part) in keys.parts.iter().enumerate() {
        match arg.arg_type_unwrap_cls_ok(cmd)? {
            command::cmd_arg::ArgType::Object(obj) => match part {
                SHPartialKeyPart::Name(name) => {
                    arg = obj.get_prop(name).or(obj.get_additional_item()?).ok_or(
                        AzUserUsageError::InvalidArgumentValueError(format!(
                            "Object type argument {} has no internal property named '{}'",
                            keys.split_at(i),
                            name
                        )),
                    )?;
                }
                SHPartialKeyPart::Index(_) | SHPartialKeyPart::AppendIndex(_) => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                        "Object type argument {} cannot support internal access by index",
                        keys.split_at(i)
                    ))
                    .into());
                }
            },
            command::cmd_arg::ArgType::Array(arr) => match part {
                SHPartialKeyPart::Index(_) | SHPartialKeyPart::AppendIndex(_) => {
                    arg = arr.item_ok()?;
                }
                SHPartialKeyPart::Name(name) => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                        "Array type argument {} has no internal property named '{}'",
                        keys.split_at(i),
                        name
                    ))
                    .into());
                }
            },
            command::cmd_arg::ArgType::Str(_) => {
                return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "String type argument {} cannot support internal access",
                    keys.split_at(i)
                ))
                .into());
            }
            command::cmd_arg::ArgType::Number(_) => {
                return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Number type argument {} cannot support internal access",
                    keys.split_at(i)
                ))
                .into());
            }
            command::cmd_arg::ArgType::Boolean(_) => {
                return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Boolean type argument {} cannot support internal access",
                    keys.split_at(i)
                ))
                .into());
            }
            command::cmd_arg::ArgType::AnyType(_) => {
                return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Any type argument {} cannot support internal access",
                    keys.split_at(i)
                ))
                .into());
            }
            command::cmd_arg::ArgType::Cls(_) => {
                unreachable!("cls type should be unwrapped before");
            }
        }
    }
    Ok(arg)
}
