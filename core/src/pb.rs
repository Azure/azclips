mod command;
mod component;

use core::fmt;
use serde_json::Value;

use crate::error::{AzInternalError, AzResult, AzUserUsageError};

pub use command::*;
pub use component::*;

impl command::CrsArg {
    pub fn arg_type_ok(&self) -> Result<&command::crs_arg::ArgType, AzInternalError> {
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

impl fmt::Display for command::crs_arg::ArgType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            command::crs_arg::ArgType::String(str_type) => match &str_type.format {
                None => write!(f, "[string]"),
                Some(format) => match format {
                    command::crs_string_arg::Format::Binary(_) => write!(f, "[file]"),
                    command::crs_string_arg::Format::Byte(_) => write!(f, "[base64-string]"),
                    command::crs_string_arg::Format::String(_) => write!(f, "[string]"),
                    command::crs_string_arg::Format::Duration(_) => write!(f, "[duration]"),
                    command::crs_string_arg::Format::Date(_) => write!(f, "[date]"),
                    command::crs_string_arg::Format::DateTime(_) => write!(f, "[datetime]"),
                    command::crs_string_arg::Format::Time(_) => write!(f, "[time]"),
                    command::crs_string_arg::Format::Uuid(_) => write!(f, "[uuid]"),
                    command::crs_string_arg::Format::Password(_) => write!(f, "[secret-string]"),
                    command::crs_string_arg::Format::SubscriptionId(_) => {
                        write!(f, "[subscription]")
                    }
                    command::crs_string_arg::Format::ResourceGroupName(_) => {
                        write!(f, "[string]")
                    }
                    command::crs_string_arg::Format::ResourceId(_) => write!(f, "[id]"),
                    command::crs_string_arg::Format::ResourceLocation(_) => write!(f, "[location]"),
                },
            },
            command::crs_arg::ArgType::Number(number_type) => match &number_type.format {
                None => write!(f, "[int]"),
                Some(format) => match format {
                    command::crs_number_arg::Format::Int32(_) => write!(f, "[int]"),
                    command::crs_number_arg::Format::Int64(_) => write!(f, "[int]"),
                    command::crs_number_arg::Format::Uint32(_) => write!(f, "[uint]"),
                    command::crs_number_arg::Format::Uint64(_) => write!(f, "[uint]"),
                    command::crs_number_arg::Format::Float(_) => write!(f, "[float]"),
                    command::crs_number_arg::Format::Double(_) => write!(f, "[float]"),
                },
            },
            command::crs_arg::ArgType::Boolean(_) => write!(f, "boolean"),
            command::crs_arg::ArgType::Object(object_type) => {
                if object_type.props.is_empty() && object_type.additional_props.is_some() {
                    write!(f, "[dict]")
                } else {
                    write!(f, "[object]")
                }
            }
            command::crs_arg::ArgType::Array(_) => write!(f, "[list]"),
            command::crs_arg::ArgType::AnyType(_) => write!(f, "[any]"),
            command::crs_arg::ArgType::Cls(cls_type) => {
                write!(f, "[{}]", cls_type.cls_type)
            }
        }
    }
}

impl command::CrsObjectArgAdditionalProperties {
    pub fn item_ok(&self) -> Result<&command::CrsArg, AzInternalError> {
        let item = self
            .item
            .as_ref()
            .ok_or(AzInternalError::InvalidArgumentDefinitionError(
                "Missing item for additional properties".to_string(),
            ))?;
        Ok(item)
    }
}

impl command::CrsObjectArg {
    /// return the property argument by given the name or alias, if not found, return None
    /// This method will not check the additional properties
    pub fn get_prop<'a>(&'a self, name_or_alias: &'_ str) -> Option<&'a command::CrsArg> {
        self.props.iter().find(|&prop| {
            prop.name == name_or_alias || prop.options.iter().any(|o| o == name_or_alias)
        })
    }
    /// return the item argument for the additional properties
    pub fn get_additional_item(&self) -> Result<Option<&command::CrsArg>, AzInternalError> {
        match self.additional_props.as_ref() {
            Some(additional) => Ok(Some(additional.item_ok()?)),
            None => Ok(None),
        }
    }
}

impl command::CrsArrayArg {
    pub fn item_ok(&self) -> Result<&command::CrsArg, AzInternalError> {
        let item = self
            .item
            .as_ref()
            .ok_or(AzInternalError::InvalidArgumentDefinitionError(
                "Missing item for array".to_string(),
            ))?;
        Ok(item)
    }
}

impl command::CrsArgEnum {
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
            if let (Value::String(s), Value::String(t)) = (&v, &value) {
                if !self.case_sensitive && s.eq_ignore_ascii_case(t) {
                    return Ok(v);
                }
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

impl fmt::Display for command::CrsArgEnum {
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

impl TryFrom<command::CrsArgBlank> for Value {
    type Error = AzInternalError;

    fn try_from(value: command::CrsArgBlank) -> Result<Self, Self::Error> {
        let value = serde_json::from_str(value.value.as_str()).or(Err(
            AzInternalError::InvalidArgumentDefinitionError(
                "Failed to parse value from the blank value".to_string(),
            ),
        ))?;
        Ok(value)
    }
}

impl TryFrom<command::CrsArgDefault> for Value {
    type Error = AzInternalError;

    fn try_from(value: command::CrsArgDefault) -> Result<Self, Self::Error> {
        let value = serde_json::from_str(value.value.as_str()).or(Err(
            AzInternalError::InvalidArgumentDefinitionError(
                "Failed to parse value from the default value".to_string(),
            ),
        ))?;
        Ok(value)
    }
}

impl TryFrom<command::CrsArgEnumItem> for Value {
    type Error = AzInternalError;

    fn try_from(value: command::CrsArgEnumItem) -> Result<Self, Self::Error> {
        let value = serde_json::from_str(value.value.as_str()).or(Err(
            AzInternalError::InvalidArgumentDefinitionError(
                "Failed to parse value from the enum item".to_string(),
            ),
        ))?;
        Ok(value)
    }
}

impl command::CrsInterface {
    pub fn as_command_ok(&self) -> &command::CrsCommand {
        match &self.level.as_ref().expect("Level not found") {
            crs_interface::Level::Command(command) => command,
            _ => panic!("Command not found"),
        }
    }

    pub fn as_command_group_ok(&self) -> &command::CrsCommandGroup {
        match &self.level.as_ref().expect("Level not found") {
            crs_interface::Level::CommandGroup(command_group) => command_group,
            _ => panic!("Command group not found"),
        }
    }

    pub fn as_component_ok(&self) -> &command::CrsComponent {
        match &self.level.as_ref().expect("Level not found") {
            crs_interface::Level::Component(component) => component,
            _ => panic!("Component not found"),
        }
    }

    pub fn name(&self) -> &str {
        match &self.level.as_ref().expect("Level not found") {
            crs_interface::Level::Command(command) => command.name.as_str(),
            crs_interface::Level::CommandGroup(command_group) => command_group.name.as_str(),
            crs_interface::Level::Component(component) => component.name.as_str(),
        }
    }

    pub fn uri(&self) -> &str {
        match &self.level.as_ref().expect("Level not found") {
            crs_interface::Level::Command(command) => command.uri.as_str(),
            crs_interface::Level::CommandGroup(command_group) => command_group.uri.as_str(),
            crs_interface::Level::Component(component) => component.uri.as_str(),
        }
    }

    pub fn help(&self) -> Option<&command::CrsHelp> {
        match &self.level.as_ref().expect("Level not found") {
            crs_interface::Level::Command(command) => command.help.as_ref(),
            crs_interface::Level::CommandGroup(command_group) => command_group.help.as_ref(),
            crs_interface::Level::Component(component) => component.help.as_ref(),
        }
    }
}
