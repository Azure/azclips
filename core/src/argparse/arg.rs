use std::{fmt, sync::LazyLock};

use serde_json::{Map, Value};

use crate::{
    error::{AzError, AzInternalError, AzResult, AzUserUsageError},
    pb::{
        crs_arg, CrsArg, CrsArgBlank, CrsArgEnum, CrsArgEnumItem, CrsArgPromptInput, CrsArrayArg,
        CrsCommand, CrsObjectArg,
    },
};

use super::{SHPartialKeyPart, SHPartialKeys};

/// This wrapper support unwrapping the cls type of the argument
#[derive(Debug, Clone)]
pub struct CrsArgWrapper<'a> {
    cmd: Option<&'a CrsCommand>,
    inner: &'a CrsArg,
    unwrapped_arg_type: &'a crs_arg::ArgType,
    prefix_keys: SHPartialKeys,
    normalized_name: Option<String>,
}

impl<'a> CrsArgWrapper<'a> {
    /// create a new CrsArgWrapper
    ///
    /// # Arguments
    ///
    /// * `cmd` - The command to wrap, for the global argument, the cmd is None, otherwise it's the command to wrap
    /// * `inner` - The inner argument to wrap
    ///
    /// # Returns
    ///
    /// A new CrsArgWrapper
    pub fn new(cmd: Option<&'a CrsCommand>, inner: &'a CrsArg) -> Result<Self, AzInternalError> {
        let arg_type = get_unwrapped_arg_type(cmd, inner)?;
        Ok(Self {
            cmd,
            inner,
            unwrapped_arg_type: arg_type,
            prefix_keys: SHPartialKeys::from_parts(&[SHPartialKeyPart::Name(
                inner.name.to_string(),
            )]),
            normalized_name: Some(inner.name.to_string()),
        })
    }

    pub fn build_by_keys(cmd: &'a CrsCommand, keys: &SHPartialKeys) -> AzResult<Self> {
        let mut parts = keys.parts();
        let key = parts
            .next()
            .ok_or(AzInternalError::InvalidPartialKeysError(format!(
                "Partial keys is empty: {}",
                keys
            )))?;
        let arg = match key {
            SHPartialKeyPart::Name(name) => cmd
                .args
                .iter()
                .find(|arg| arg.name.eq_ignore_ascii_case(name))
                .ok_or(AzInternalError::InvalidPartialKeysError(format!(
                    "Argument name not found: {}",
                    name
                )))?,
            _ => {
                return Err(AzInternalError::InvalidPartialKeysError(format!(
                    "Partial keys is not supported: {}",
                    keys
                ))
                .into());
            }
        };
        let wrapper = Self::new(Some(cmd), arg)?;
        wrapper.retrieve_sub_arg(parts)
    }
}

impl fmt::Display for CrsArgWrapper<'_> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.prefix_keys)
    }
}

impl CrsArgWrapper<'_> {
    pub fn is_nullable(&self) -> bool {
        self.inner.nullable
    }

    // get the blank value of the argument
    // if the argument is boolean type, use the default blank value which is "true"
    pub fn blank(&self) -> Option<&CrsArgBlank> {
        self.inner
            .blank
            .as_ref()
            .or_else(|| match self.inner.arg_type {
                // apply default blank value for boolean type
                Some(crs_arg::ArgType::Boolean(_)) => Some(&BOOLEAN_ARG_BLANK_DEFAULT),
                _ => None,
            })
    }

    pub fn prompt(&self) -> Option<&CrsArgPromptInput> {
        // TODO: check if the stdin is terminal
        self.inner.prompt.as_ref()
    }

    pub fn unwrapped_arg_type(&self) -> &crs_arg::ArgType {
        self.unwrapped_arg_type
    }

    pub fn prefix_keys(&self) -> &SHPartialKeys {
        &self.prefix_keys
    }

    pub fn arg(&self) -> &CrsArg {
        self.inner
    }

    /// the normalized name of the argument, it uses the argument name instead of the option alias,
    /// for name of additional property or array item, it's null and the default value will be used
    pub fn normalized_name_or(&self, default: impl ToString) -> String {
        self.normalized_name
            .as_deref()
            .map(|name| name.to_string())
            .unwrap_or_else(|| default.to_string())
    }
}

impl CrsArgWrapper<'_> {
    /// Verify the value of the argument, apply enum transform if needed
    pub fn verify_value(&self, mut value: Value) -> AzResult<Value> {
        let arg_type = self.unwrapped_arg_type;

        // apply enum transform
        if let Some(v) = value.as_str() {
            match arg_type {
                crs_arg::ArgType::String(str_type) => {
                    if let Some(e) = str_type.r#enum.as_ref() {
                        if let Some(v) = e.find_value_by_name(v)? {
                            value = v;
                        }
                    }
                }
                crs_arg::ArgType::Number(number_type) => {
                    if let Some(e) = number_type.r#enum.as_ref() {
                        if let Some(v) = e.find_value_by_name(v)? {
                            value = v;
                        }
                    }
                }
                crs_arg::ArgType::Boolean(_) => {
                    if let Some(v) = BOOLEAN_ARG_ENUM_DEFAULT.find_value_by_name(v)? {
                        value = v;
                    }
                }
                crs_arg::ArgType::AnyType(_)
                | crs_arg::ArgType::Object(_)
                | crs_arg::ArgType::Array(_) => {
                    // do nothing
                }
                crs_arg::ArgType::Cls(_) => {
                    unreachable!("cls type should be unwrapped before");
                }
            }
        }

        // verify value
        if value.is_null() && !self.is_nullable() {
            return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                "'{}' argument is not allowed to be null",
                self
            ))
            .into());
        } else if !value.is_null() {
            match arg_type {
                crs_arg::ArgType::AnyType(_) => {
                    // do nothing
                }
                crs_arg::ArgType::String(str_type) => {
                    if let Some(arg_enum) = str_type.r#enum.as_ref() {
                        value = arg_enum.validate_value(value).map_err(|e| {
                            if let AzError::UserUsage(
                                AzUserUsageError::InvalidArgumentValueError(e),
                            ) = e
                            {
                                AzUserUsageError::InvalidArgumentValueError(format!(
                                    "'{}' argument has invalid value: {}",
                                    self, e
                                ))
                                .into()
                            } else {
                                e
                            }
                        })?;
                    }
                    if !value.is_string() {
                        self.invalid_value_type()?;
                    }
                }
                crs_arg::ArgType::Number(number_type) => {
                    if let Some(arg_enum) = number_type.r#enum.as_ref() {
                        value = arg_enum.validate_value(value).map_err(|e| {
                            if let AzError::UserUsage(
                                AzUserUsageError::InvalidArgumentValueError(e),
                            ) = e
                            {
                                AzUserUsageError::InvalidArgumentValueError(format!(
                                    "'{}' argument has invalid value: {}",
                                    self, e
                                ))
                                .into()
                            } else {
                                e
                            }
                        })?;
                    }
                    if !value.is_number() {
                        self.invalid_value_type()?;
                    }
                }
                crs_arg::ArgType::Boolean(_) => {
                    value = BOOLEAN_ARG_ENUM_DEFAULT
                        .validate_value(value)
                        .map_err(|e| {
                            if let AzError::UserUsage(
                                AzUserUsageError::InvalidArgumentValueError(e),
                            ) = e
                            {
                                AzUserUsageError::InvalidArgumentValueError(format!(
                                    "'{}' argument has invalid value: {}",
                                    self, e
                                ))
                                .into()
                            } else {
                                e
                            }
                        })?;
                    if !value.is_boolean() {
                        self.invalid_value_type()?;
                    }
                }

                crs_arg::ArgType::Object(_) => {
                    if !value.is_object() {
                        self.invalid_value_type()?;
                    }
                    // verify internal properties
                    let obj = value.as_object_mut().unwrap();
                    for (k, v) in obj.iter_mut() {
                        let sub_arg =
                            self.retrieve_sub_arg([&SHPartialKeyPart::Name(k.clone())])?;
                        // use take to get the value without cloning and replace it with the verified value
                        let temp_value = std::mem::take(v);
                        *v = sub_arg.verify_value(temp_value)?;
                    }
                }
                crs_arg::ArgType::Array(_) => {
                    if !value.is_array() {
                        self.invalid_value_type()?;
                    }
                    // verify internal items
                    let arr = value.as_array_mut().unwrap();
                    for (i, v) in arr.iter_mut().enumerate() {
                        let sub_arg =
                            self.retrieve_sub_arg([&SHPartialKeyPart::Index(i as u16)])?;
                        // use take to get the value without cloning and replace it with the verified value
                        let temp_value = std::mem::take(v);
                        *v = sub_arg.verify_value(temp_value)?;
                    }
                }

                crs_arg::ArgType::Cls(_) => {
                    unreachable!("cls type should be unwrapped before");
                }
            }
        }
        Ok(value)
    }

    pub fn invalid_value_type(&self) -> Result<(), AzUserUsageError> {
        Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "'{}' argument has invalid value type, expect {} type value",
            self, self.unwrapped_arg_type
        )))
    }
}

impl CrsArgWrapper<'_> {
    pub fn retrieve_sub_arg<'b>(
        &self,
        parts: impl IntoIterator<Item = &'b SHPartialKeyPart>,
    ) -> AzResult<Self> {
        let mut arg = self.inner;
        let mut arg_type = self.unwrapped_arg_type;
        let mut prefix_keys = self.prefix_keys.clone();
        let mut param_name = self.normalized_name.clone();
        for part in parts.into_iter() {
            match arg_type {
                crs_arg::ArgType::Object(obj) => match part {
                    SHPartialKeyPart::Name(name) => {
                        if let Some(prop) = obj.get_prop(name.as_str()) {
                            arg = prop;
                            param_name = Some(arg.name.to_string());
                        } else if let Some(item) = obj.get_additional_item()? {
                            arg = item;
                            // the param name for item should be null, it should be provided by the user
                            param_name = None;
                        } else {
                            return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                                "'{}' argument is object type and has no internal property '{}'",
                                prefix_keys, name
                            ))
                            .into());
                        }
                    }
                    SHPartialKeyPart::Index(_) => {
                        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                            "'{}' argument is object type and cannot support internal access by index {}",
                            prefix_keys,
                            part
                        ))
                        .into());
                    }
                },
                crs_arg::ArgType::Array(arr) => match part {
                    SHPartialKeyPart::Index(_) => {
                        arg = arr.item_ok()?;
                        // the param name for item should be null, it should be provided by the user
                        param_name = None;
                    }
                    SHPartialKeyPart::Name(name) => {
                        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                            "'{}' argument is array type and has no internal property '{}'",
                            prefix_keys, name
                        ))
                        .into());
                    }
                },
                crs_arg::ArgType::String(_) => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                        "'{}' argument is string type and cannot support internal access by '{}'",
                        prefix_keys, part
                    ))
                    .into());
                }
                crs_arg::ArgType::Number(_) => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                        "'{}' argument is number type and cannot support internal access by '{}'",
                        prefix_keys, part
                    ))
                    .into());
                }
                crs_arg::ArgType::Boolean(_) => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                        "'{}' argument is boolean type and cannot support internal access by '{}'",
                        prefix_keys, part
                    ))
                    .into());
                }
                crs_arg::ArgType::AnyType(_) => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(format!(
                        "'{}' argument is any type and cannot support internal access by '{}'",
                        prefix_keys, part
                    ))
                    .into());
                }
                crs_arg::ArgType::Cls(_) => {
                    unreachable!("cls type should be unwrapped before");
                }
            }
            arg_type = get_unwrapped_arg_type(self.cmd, arg)?;
            prefix_keys.push(part.to_owned());
        }

        Ok(Self {
            cmd: self.cmd,
            inner: arg,
            unwrapped_arg_type: arg_type,
            prefix_keys,
            normalized_name: param_name,
        })
    }

    /// Normalizes the property names in a JSON value according to the argument definition.
    ///
    /// This function recursively processes JSON values to ensure that object keys match the defined
    /// argument name instead of the option alias. For additional properties or array items, it will
    /// keep the original name.
    pub fn normalize_value_names(&self, value: Value) -> Value {
        match value {
            Value::Object(obj) => {
                if matches!(self.inner.arg_type, Some(crs_arg::ArgType::Object(_))) {
                    let mut new_obj = Map::new();
                    for (k, v) in obj.into_iter() {
                        let sub_arg = self
                            .retrieve_sub_arg([&SHPartialKeyPart::Name(k.clone())])
                            .unwrap();
                        let sub_arg_name = sub_arg.normalized_name_or(k);
                        let v = sub_arg.normalize_value_names(v);
                        new_obj.insert(sub_arg_name, v);
                    }
                    Value::Object(new_obj)
                } else {
                    Value::Object(obj)
                }
            }
            Value::Array(arr) => {
                if matches!(self.inner.arg_type, Some(crs_arg::ArgType::Array(_))) {
                    let mut new_arr = Vec::new();
                    for (i, v) in arr.into_iter().enumerate() {
                        let sub_arg = self
                            .retrieve_sub_arg([&SHPartialKeyPart::Index(i as u16)])
                            .unwrap();
                        let v = sub_arg.normalize_value_names(v);
                        new_arr.push(v);
                    }
                    Value::Array(new_arr)
                } else {
                    Value::Array(arr)
                }
            }
            _ => value,
        }
    }
}

/// get the unwrapped arg type of the argument, when the argument is a class type, then cmd should be not None
fn get_unwrapped_arg_type<'a>(
    cmd: Option<&'a CrsCommand>,
    arg: &'a CrsArg,
) -> Result<&'a crs_arg::ArgType, AzInternalError> {
    match arg.arg_type_ok()? {
        crs_arg::ArgType::Cls(typ) => {
            let cmd = cmd.ok_or(AzInternalError::InvalidArgumentDefinitionError(format!(
                "Missing command definition to unwrap class type argument: {}",
                typ.cls_type
            )))?;
            let cls_type = &typ.cls_type;
            for arg in cmd.args.iter() {
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

fn retrieve_cls_arg_type_in_obj<'a>(
    obj: &'a CrsObjectArg,
    cls_type: &'_ str,
) -> Result<Option<&'a crs_arg::ArgType>, AzInternalError> {
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
    array: &'a CrsArrayArg,
    cls_type: &'_ str,
) -> Result<Option<&'a crs_arg::ArgType>, AzInternalError> {
    let item = array.item_ok()?;
    if let Some(arg_type) = retrieve_cls_arg_type(item, cls_type)? {
        return Ok(Some(arg_type));
    }
    Ok(None)
}

fn retrieve_cls_arg_type<'a>(
    arg: &'a CrsArg,
    cls_type: &'_ str,
) -> Result<Option<&'a crs_arg::ArgType>, AzInternalError> {
    let arg_type = arg.arg_type_ok()?;
    match arg_type {
        crs_arg::ArgType::Object(obj) => {
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
        crs_arg::ArgType::Array(array) => {
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
    Ok(None)
}

static BOOLEAN_ARG_BLANK_DEFAULT: LazyLock<CrsArgBlank> = LazyLock::new(|| CrsArgBlank {
    value: "true".to_string(),
});

static BOOLEAN_ARG_ENUM_DEFAULT: LazyLock<CrsArgEnum> = LazyLock::new(|| CrsArgEnum {
    items: vec![
        CrsArgEnumItem {
            name: "true".to_string(),
            value: "true".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "t".to_string(),
            value: "true".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "yes".to_string(),
            value: "true".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "y".to_string(),
            value: "true".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "false".to_string(),
            value: "false".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "f".to_string(),
            value: "false".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "no".to_string(),
            value: "false".to_string(),
            internal: false,
        },
        CrsArgEnumItem {
            name: "n".to_string(),
            value: "false".to_string(),
            internal: false,
        },
    ],
    support_extension: false,
    case_sensitive: false,
});
