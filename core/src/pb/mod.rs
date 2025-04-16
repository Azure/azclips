pub mod command;
pub mod component;

use core::fmt;
use serde_json::Value;

use crate::error::{AzInternalError, AzResult, AzUserUsageError};

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
}

impl fmt::Display for command::cmd_arg::ArgType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            command::cmd_arg::ArgType::Str(str_type) => match &str_type.format {
                None => write!(f, "[string]"),
                Some(format) => match format {
                    command::cmd_string_arg::Format::Binary(_) => write!(f, "[file]"),
                    command::cmd_string_arg::Format::Byte(_) => write!(f, "[base64-string]"),
                    command::cmd_string_arg::Format::String(_) => write!(f, "[string]"),
                    command::cmd_string_arg::Format::Duration(_) => write!(f, "[duration]"),
                    command::cmd_string_arg::Format::Date(_) => write!(f, "[date]"),
                    command::cmd_string_arg::Format::DateTime(_) => write!(f, "[datetime]"),
                    command::cmd_string_arg::Format::Time(_) => write!(f, "[time]"),
                    command::cmd_string_arg::Format::Uuid(_) => write!(f, "[uuid]"),
                    command::cmd_string_arg::Format::Password(_) => write!(f, "[secret-string]"),
                    command::cmd_string_arg::Format::SubscriptionId(_) => {
                        write!(f, "[subscription]")
                    }
                    command::cmd_string_arg::Format::ResourceGroupName(_) => {
                        write!(f, "[string]")
                    }
                    command::cmd_string_arg::Format::ResourceId(_) => write!(f, "[id]"),
                    command::cmd_string_arg::Format::ResourceLocation(_) => write!(f, "[location]"),
                },
            },
            command::cmd_arg::ArgType::Number(number_type) => match &number_type.format {
                None => write!(f, "[int]"),
                Some(format) => match format {
                    command::cmd_number_arg::Format::Int32(_) => write!(f, "[int]"),
                    command::cmd_number_arg::Format::Int64(_) => write!(f, "[int]"),
                    command::cmd_number_arg::Format::Uint32(_) => write!(f, "[uint]"),
                    command::cmd_number_arg::Format::Uint64(_) => write!(f, "[uint]"),
                    command::cmd_number_arg::Format::Float(_) => write!(f, "[float]"),
                    command::cmd_number_arg::Format::Double(_) => write!(f, "[float]"),
                },
            },
            command::cmd_arg::ArgType::Boolean(_) => write!(f, "boolean"),
            command::cmd_arg::ArgType::Object(object_type) => {
                if object_type.props.is_empty() && object_type.additional_props.is_some() {
                    write!(f, "[dict]")
                } else {
                    write!(f, "[object]")
                }
            }
            command::cmd_arg::ArgType::Array(_) => write!(f, "[list]"),
            command::cmd_arg::ArgType::AnyType(_) => write!(f, "[any]"),
            command::cmd_arg::ArgType::Cls(cls_type) => {
                write!(f, "[{}]", cls_type.cls_type)
            }
        }
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

impl command::CmdObjectArg {
    /// return the property argument by given the name or alias, if not found, return None
    /// This method will not check the additional properties
    pub fn get_prop<'a>(&'a self, name_or_alias: &'_ str) -> Option<&'a command::CmdArg> {
        for prop in self.props.iter() {
            if prop.name == name_or_alias || prop.options.iter().any(|o| o == name_or_alias) {
                return Some(prop);
            }
        }
        None
    }
    /// return the item argument for the additional properties
    pub fn get_additional_item(&self) -> Result<Option<&command::CmdArg>, AzInternalError> {
        match self.additional_props.as_ref() {
            Some(additional) => Ok(Some(additional.item_ok()?)),
            None => Ok(None),
        }
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

impl command::CmdArgEnum {
    pub fn find_value_by_name(&self, str: &str) -> Result<Option<Value>, AzInternalError> {
        for item in self.items.iter() {
            if item.name == str || (!self.case_sensitive && item.name.eq_ignore_ascii_case(str)) {
                let value = item.to_owned().try_into()?;
                return Ok(Some(value));
            }
        }
        Ok(None)
    }

    pub fn validate_value(&self, value: Value) -> AzResult<Value> {
        for item in self.items.iter() {
            let v = item.to_owned().try_into()?;
            if v == value {
                return Ok(v);
            }
            // case insensitive match
            match (&v, &value) {
                (Value::String(s), Value::String(t)) => {
                    if !self.case_sensitive && s.eq_ignore_ascii_case(t) {
                        return Ok(v);
                    }
                }
                _ => (),
            }
        }

        if self.support_extension {
            return Ok(value);
        }
        Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "invalid value {}, accept enum items: {}",
            value, self
        ))
        .into())
    }
}

impl fmt::Display for command::CmdArgEnum {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "{}",
            self.items
                .iter()
                .map(|i| i.name.as_str())
                .collect::<Vec<_>>()
                .join(", ")
        )
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

impl TryFrom<command::CmdArgEnumItem> for Value {
    type Error = AzInternalError;

    fn try_from(value: command::CmdArgEnumItem) -> Result<Self, Self::Error> {
        let value = serde_json::from_str(value.value.as_str()).or(Err(
            AzInternalError::InvalidArgumentDefinitionError(
                "Failed to parse value from the enum item".to_string(),
            ),
        ))?;
        Ok(value)
    }
}
