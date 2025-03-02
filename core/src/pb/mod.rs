pub mod command;
pub mod context;
pub mod module;

use serde_json::Value;

use crate::error::AzInternalError;

impl command::CmdArg {
    pub fn arg_type_ok(&self) -> Result<&command::cmd_arg::ArgType, AzInternalError> {
        let arg_type =
            self.arg_type
                .as_ref()
                .ok_or(AzInternalError::InvalidArgumentDefinitionError(format!(
                    "Missing argument type for argument: {}",
                    self.var_name
                )))?;
        Ok(arg_type)
    }

    /// return the internal argument type without the cls type
    pub fn arg_type_unwrap_cls_ok<'a>(
        &'a self,
        cmd: &'a command::CmdCommand,
    ) -> Result<&'a command::cmd_arg::ArgType, AzInternalError> {
        let mut arg_type = self.arg_type_ok()?;
        if let command::cmd_arg::ArgType::Cls(typ) = arg_type {
            let cls_type = &typ.cls_type;
            arg_type = retrieve_cls_arg_type_in_cmd(cmd, cls_type)?.ok_or(
                AzInternalError::InvalidArgumentDefinitionError(format!(
                    "Missing argument definition for argument class: {}",
                    cls_type
                )),
            )?;
        }
        Ok(arg_type)
    }
}

impl command::CmdObjectArgAdditionalProperties {
    pub fn item_ok(&self) -> Result<&command::CmdArg, AzInternalError> {
        let item = self
            .item
            .as_ref()
            .ok_or(AzInternalError::InvalidArgumentDefinitionError(
                "Missing item for additional properties".to_string(),
            ))?;
        Ok(item)
    }
}

impl command::CmdArrayArg {
    pub fn item_ok(&self) -> Result<&command::CmdArg, AzInternalError> {
        let item = self
            .item
            .as_ref()
            .ok_or(AzInternalError::InvalidArgumentDefinitionError(format!(
                "Missing item for array"
            )))?;
        Ok(item)
    }
}

impl command::CmdNumberArg {
    pub fn format_ok(&self) -> Result<&command::cmd_number_arg::Format, AzInternalError> {
        let format =
            self.format
                .as_ref()
                .ok_or(AzInternalError::InvalidArgumentDefinitionError(
                    "Missing format for number argument".to_string(),
                ))?;
        Ok(format)
    }
}

impl TryFrom<command::CmdArgBlank> for Value {
    type Error = AzInternalError;

    fn try_from(value: command::CmdArgBlank) -> Result<Self, Self::Error> {
        let value = serde_json::from_str(value.value.as_str()).or(Err(
            AzInternalError::InvalidArgumentDefinitionError(
                "Failed to parse value from the blank value".to_string(),
            ),
        ))?;
        Ok(value)
    }
}

impl TryFrom<command::CmdArgDefault> for Value {
    type Error = AzInternalError;

    fn try_from(value: command::CmdArgDefault) -> Result<Self, Self::Error> {
        let value = serde_json::from_str(value.value.as_str()).or(Err(
            AzInternalError::InvalidArgumentDefinitionError(
                "Failed to parse value from the default value".to_string(),
            ),
        ))?;
        Ok(value)
    }
}

fn retrieve_cls_arg_type_in_cmd<'a>(
    cmd: &'a command::CmdCommand,
    cls_type: &'_ String,
) -> Result<Option<&'a command::cmd_arg::ArgType>, AzInternalError> {
    for arg in cmd.args.iter() {
        if let Some(arg_type) = retrieve_cls_arg_type(arg, cls_type)? {
            return Ok(Some(arg_type));
        }
    }
    Ok(None)
}

fn retrieve_cls_arg_type_in_obj<'a>(
    obj: &'a command::CmdObjectArg,
    cls_type: &'_ String,
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
    cls_type: &'_ String,
) -> Result<Option<&'a command::cmd_arg::ArgType>, AzInternalError> {
    let item = array.item_ok()?;
    if let Some(arg_type) = retrieve_cls_arg_type(item, cls_type)? {
        return Ok(Some(arg_type));
    }
    Ok(None)
}

fn retrieve_cls_arg_type<'a>(
    arg: &'a command::CmdArg,
    cls_type: &'_ String,
) -> Result<Option<&'a command::cmd_arg::ArgType>, AzInternalError> {
    let arg_type = arg.arg_type_ok()?;
    match arg_type {
        command::cmd_arg::ArgType::Object(obj) => {
            if obj.cls_type == *cls_type {
                return Ok(Some(arg_type));
            }
            if let Some(arg_type) = retrieve_cls_arg_type_in_obj(obj, cls_type)? {
                return Ok(Some(arg_type));
            }
        }
        command::cmd_arg::ArgType::Array(array) => {
            if array.cls_type == *cls_type {
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
