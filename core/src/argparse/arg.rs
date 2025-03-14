use std::{fmt, sync::LazyLock};

use crate::{
    error::{AzInternalError, AzResult, AzUserUsageError},
    pb::command,
};

use super::partial::{SHPartialKeyPart, SHPartialKeys};

/// This wrapper support unwrapping the cls type of the argument
#[derive(Debug, Clone)]
pub struct CmdArgWrapper<'a> {
    cmd: &'a command::CmdCommand,
    inner: &'a command::CmdArg,
    pub(super) prefix_keys: SHPartialKeys,
}

impl<'a> CmdArgWrapper<'a> {
    pub fn new(cmd: &'a command::CmdCommand, inner: &'a command::CmdArg) -> Self {
        Self {
            cmd,
            inner,
            prefix_keys: SHPartialKeys {
                parts: vec![SHPartialKeyPart::Name(inner.name.to_string())],
            },
        }
    }
}

impl<'a> fmt::Display for CmdArgWrapper<'a> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.prefix_keys)
    }
}

impl CmdArgWrapper<'_> {
    pub fn is_nullable(&self) -> bool {
        return self.inner.nullable;
    }

    pub fn blank(&self) -> Option<&command::CmdArgBlank> {
        return self
            .inner
            .blank
            .as_ref()
            .or_else(|| match self.inner.arg_type {
                // apply default blank value for boolean type
                Some(command::cmd_arg::ArgType::Boolean(_)) => Some(&BOOLEAN_ARG_BLANK_DEFAULT),
                _ => None,
            });
    }

    pub fn prompt(&self) -> Option<&command::CmdArgPromptInput> {
        return self.inner.prompt.as_ref();
    }
}

impl<'a> CmdArgWrapper<'a> {
    /// return the internal argument type without the cls type
    pub fn arg_type_unwrap_cls_ok(&self) -> Result<&'a command::cmd_arg::ArgType, AzInternalError> {
        match self.inner.arg_type_ok()? {
            command::cmd_arg::ArgType::Cls(typ) => {
                let cls_type = &typ.cls_type;
                for arg in self.cmd.args.iter() {
                    if let Some(arg_type) = retrieve_cls_arg_type(arg, cls_type)? {
                        return Ok(arg_type);
                    }
                }
                Err(AzInternalError::InvalidArgumentDefinitionError(format!(
                    "Missing argument definition for argument class: {}",
                    cls_type
                )))
            }
            arg_type => Ok(arg_type),
        }
    }

    pub fn retrieve_internal_arg_by_keys(&self, keys: &SHPartialKeys) -> AzResult<Self> {
        let mut arg = self.inner;
        for (i, part) in keys.parts.iter().enumerate() {
            match self.arg_type_unwrap_cls_ok()? {
                command::cmd_arg::ArgType::Object(obj) => match part {
                    SHPartialKeyPart::Name(name) => {
                        arg = obj
                            .get_prop(name.as_str())
                            .or(obj.get_additional_item()?)
                            .ok_or(AzUserUsageError::InvalidArgumentValueError(format!(
                                "Object type argument {} has no internal property named '{}'",
                                keys.split_at(i),
                                name
                            )))?;
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

        Ok(Self {
            cmd: self.cmd,
            inner: arg,
            prefix_keys: self.prefix_keys.to_owned().with_extend(keys.to_owned()),
        })
    }
}

fn retrieve_cls_arg_type_in_obj<'a>(
    obj: &'a command::CmdObjectArg,
    cls_type: &'_ str,
) -> Result<Option<&'a command::cmd_arg::ArgType>, AzInternalError> {
    for arg in obj.props.iter() {
        if let Some(arg_type) = retrieve_cls_arg_type(arg, cls_type)? {
            return Ok(Some(arg_type));
        }
    }

    if let Some(additional) = obj.additional_props.as_ref() {
        let item = additional.item_ok()?;
        if let Some(arg_type) = retrieve_cls_arg_type(item, cls_type)? {
            return Ok(Some(arg_type));
        }
    }
    Ok(None)
}

fn retrieve_cls_arg_type_in_array<'a>(
    array: &'a command::CmdArrayArg,
    cls_type: &'_ str,
) -> Result<Option<&'a command::cmd_arg::ArgType>, AzInternalError> {
    let item = array.item_ok()?;
    if let Some(arg_type) = retrieve_cls_arg_type(item, cls_type)? {
        return Ok(Some(arg_type));
    }
    Ok(None)
}

fn retrieve_cls_arg_type<'a>(
    arg: &'a command::CmdArg,
    cls_type: &'_ str,
) -> Result<Option<&'a command::cmd_arg::ArgType>, AzInternalError> {
    let arg_type = arg.arg_type_ok()?;
    match arg_type {
        command::cmd_arg::ArgType::Object(obj) => {
            if obj
                .cls_type
                .as_ref()
                .is_some_and(|type_name| type_name == cls_type)
            {
                return Ok(Some(arg_type));
            }
            if let Some(arg_type) = retrieve_cls_arg_type_in_obj(obj, cls_type)? {
                return Ok(Some(arg_type));
            }
        }
        command::cmd_arg::ArgType::Array(array) => {
            if array
                .cls_type
                .as_ref()
                .is_some_and(|type_name| type_name == cls_type)
            {
                return Ok(Some(arg_type));
            }
            if let Some(arg_type) = retrieve_cls_arg_type_in_array(array, cls_type)? {
                return Ok(Some(arg_type));
            }
        }
        _ => {}
    }
    return Ok(None);
}

pub(crate) static BOOLEAN_ARG_BLANK_DEFAULT: LazyLock<command::CmdArgBlank> =
    LazyLock::new(|| command::CmdArgBlank {
        value: "true".to_string(),
    });

pub(crate) static BOOLEAN_ARG_ENUM_DEFAULT: LazyLock<command::CmdArgEnum> =
    LazyLock::new(|| command::CmdArgEnum {
        items: vec![
            command::CmdArgEnumItem {
                name: "true".to_string(),
                value: "true".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "t".to_string(),
                value: "true".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "yes".to_string(),
                value: "true".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "y".to_string(),
                value: "true".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "false".to_string(),
                value: "false".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "f".to_string(),
                value: "false".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "no".to_string(),
                value: "false".to_string(),
                internal: false,
            },
            command::CmdArgEnumItem {
                name: "n".to_string(),
                value: "false".to_string(),
                internal: false,
            },
        ],
        support_extension: false,
        case_sensitive: false,
    });
