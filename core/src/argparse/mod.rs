pub mod arg;
mod partial;
mod shorthand;

use arg::CmdArgWrapper;
use serde_json::Value;

use crate::error::{AzInternalError, AzResult, AzUserUsageError};
use crate::pb::command;

use self::partial::SHPartialKeys;
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
    name_or_alias: &str,
    values: &[String],
) -> AzResult<Option<Vec<ArgOp>>> {
    let mut matched_arg = None;
    for arg in cmd.args.iter() {
        if arg.name.eq_ignore_ascii_case(name_or_alias) {
            matched_arg = Some(arg);
            break;
        }
        if arg
            .options
            .iter()
            .any(|o| o.eq_ignore_ascii_case(name_or_alias))
        {
            matched_arg = Some(arg);
            break;
        }
    }

    if matched_arg.is_none() {
        return Ok(None);
    }
    let matched_arg = matched_arg.unwrap();

    let arg = CmdArgWrapper::new(cmd, matched_arg);
    let ops = parse_arg_values(&arg, values)?;

    Ok(Some(ops))
}

fn parse_arg_values(arg: &CmdArgWrapper, values: &[String]) -> AzResult<Vec<ArgOp>> {
    match arg.arg_type_unwrap_cls_ok()? {
        command::cmd_arg::ArgType::Str(_)
        | command::cmd_arg::ArgType::Number(_)
        | command::cmd_arg::ArgType::Boolean(_) => {
            if values.len() > 1 {
                // The simple type argument can't accept multiple values
                Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' can't accept multiple values",
                    arg
                ))
                .into())
            } else {
                let value = values.first().map(|v| v.as_str());
                let op = parse_arg_value_str(arg, value)?;
                Ok(vec![op])
            }
        }
        command::cmd_arg::ArgType::AnyType(_) => {
            if values.len() > 1 {
                // The simple type argument can't accept multiple values
                Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' can't accept multiple values",
                    arg
                ))
                .into())
            } else if values.len() == 0 {
                let op = parse_arg_value_str(arg, None)?;
                Ok(vec![op])
            } else {
                let value = values.first().unwrap();
                if let Some(_) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                    "Argument '{}' is any type arg which only support full value shorthand syntax, please don't use partial value shorthand syntax. If it's a simple string, please wrap it with single quotes.",
                    arg,
                ))
                .into());
                }
                let op = parse_arg_value_str(arg, Some(value))?;
                Ok(vec![op])
            }
        }
        command::cmd_arg::ArgType::Object(_) => {
            if values.len() == 0 {
                let op = parse_arg_value_str(arg, None)?;
                Ok(vec![op])
            } else {
                let mut ops = Vec::new();
                for value in values {
                    if let Some((key, value)) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
                        let keys = SHORTHAND_SYNTAX_PARSER.parse_partial_value_key_parts(key)?;
                        let sub_arg = arg.retrieve_internal_arg_by_keys(&keys)?;
                        let op = parse_arg_value_str(&sub_arg, value)?;
                        ops.push(op);
                    } else {
                        let op = parse_arg_value_str(arg, Some(value))?;
                        ops.push(op);
                    }
                }
                Ok(ops)
            }
        }
        command::cmd_arg::ArgType::Array(_) => {
            // here we dropped the support for space separated elements, which will cause the ambiguity
            if values.len() == 0 {
                let op = parse_arg_value_str(arg, None)?;
                Ok(vec![op])
            } else {
                let mut ops = Vec::new();
                for value in values {
                    if let Some((key, value)) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
                        let keys = SHORTHAND_SYNTAX_PARSER.parse_partial_value_key_parts(key)?;
                        let sub_arg = arg.retrieve_internal_arg_by_keys(&keys)?;
                        let op = parse_arg_value_str(&sub_arg, value)?;
                        ops.push(op);
                    } else {
                        let op = parse_arg_value_str(arg, Some(value))?;
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
    arg: &CmdArgWrapper,
    value: Option<&str>, // None for the blank input
) -> AzResult<ArgOp> {
    let arg_type = arg.arg_type_unwrap_cls_ok()?;

    // handle the blank input
    if value.is_none() {
        // allow prompt input
        if let Some(prompt) = arg.prompt() {
            return Ok(ArgOp::PromptInput(ArgPromptInputOp {
                keys: arg.prefix_keys.to_owned(),
                prompt: prompt.to_owned(),
            }));
        }

        // else use blank value instead
        let blank = arg
            .blank()
            .ok_or(AzUserUsageError::InvalidArgumentValueError(format!(
                "Argument '{}' requires a value but no value is provided",
                arg
            )))?
            .to_owned();
        let value: Value = blank.try_into()?;

        if value.is_null() && !arg.is_nullable() {
            return Err(AzInternalError::InvalidArgumentDefinitionError(format!(
                "Argument '{}' is not nullable, but the blank value is null",
                arg
            ))
            .into());
        }

        return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
            keys: arg.prefix_keys.to_owned(),
            from_blank: true,
            value,
        }));
    }

    let value = value.unwrap();

    match SHORTHAND_SYNTAX_PARSER.parse(value, Some(arg.to_owned()))? {
        SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp {
            keys: arg
                .prefix_keys
                .to_owned()
                .with_extend(keys.into_iter().rev()),
        })),
        SHResult::Value(value) => Ok(ArgOp::ValueAssign(ArgValueAssignOp {
            keys: arg.prefix_keys.to_owned(),
            from_blank: false,
            value,
        })),
    }
}

// fn format_arg_value() {}
