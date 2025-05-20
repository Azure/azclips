mod arg_fmt;

use std::collections::{hash_map::Entry, HashMap};

use arg_fmt::{
    format_array, format_number_double, format_number_float, format_number_int32,
    format_number_int64, format_number_uint32, format_number_uint64, format_object,
    format_str_binary, format_str_byte, format_str_date, format_str_datetime, format_str_duration,
    format_str_password, format_str_resource_group_name, format_str_resource_id,
    format_str_resource_location, format_str_string, format_str_subscription_id, format_str_time,
    format_str_uuid,
};
use serde_json::{map::Entry as SerdeEntry, Map, Value};

use crate::{
    argparse::{ArgValueAssignOp, CrsArgWrapper, SHPartialKeyPart, SHPartialKeys},
    error::{AzResult, AzUserUsageError},
    pb::{crs_arg, crs_number_arg, crs_string_arg, CrsCommand, CrsParams},
};

pub struct CrsParamBuilder<'a> {
    // the binary parameters will be stored as a file path in the value and converted to a binary data when building the command
    params: ParamValue,
    command: &'a CrsCommand,
}

#[derive(Debug, PartialEq, Eq)]
enum ParamValue {
    Assign(Value),
    Append(Value),
    Partial(HashMap<String, ParamValue>),
}

impl<'a> CrsParamBuilder<'a> {
    pub fn new(command: &'a CrsCommand) -> Self {
        Self {
            params: ParamValue::Partial(HashMap::new()),
            command,
        }
    }
}

impl CrsParamBuilder<'_> {
    /// apply all the value operation into a param value tree
    ///
    /// using the argument name as the key and replace all the alias
    pub fn apply(&mut self, value_op: ArgValueAssignOp) -> Result<(), AzUserUsageError> {
        if value_op.is_append && !value_op.value.is_array() {
            // append value should be an array
            return Err(AzUserUsageError::InvalidArgumentValueError(
                // TODO: improve the error message
                format!("the patch key is invalid: {}", value_op.keys),
            ));
        }

        let mut keys = value_op.keys.parts();
        let mut parent = &mut self.params;
        let mut key = keys.next().unwrap();
        let mut arg = CrsArgWrapper::build_by_keys(
            self.command,
            &SHPartialKeys::from_parts(&[key.to_owned()]),
        )
        .unwrap();
        let mut keys_next = keys.next();

        while let Some(next_key) = keys_next {
            match parent {
                ParamValue::Partial(partial) => {
                    let entry_name = arg.normalized_name_or(key);
                    parent = partial
                        .entry(entry_name)
                        .or_insert(ParamValue::Partial(HashMap::new()));
                    key = next_key;
                    arg = arg.retrieve_sub_arg(&[key.to_owned()]).unwrap();
                    keys_next = keys.next();
                }
                _ => {
                    break;
                }
            }
        }

        match parent {
            ParamValue::Partial(partial) => {
                let value = value_op.value;
                let is_append = value_op.is_append;
                let value = arg.normalize_value_names(value);
                let entry_name = arg.normalized_name_or(key);
                match partial.entry(entry_name) {
                    Entry::Vacant(entry) => {
                        if is_append {
                            entry.insert(ParamValue::Append(value));
                        } else {
                            entry.insert(ParamValue::Assign(value));
                        }
                    }
                    Entry::Occupied(mut entry) => {
                        if is_append {
                            let new_value = match entry.get() {
                                ParamValue::Partial(_) => ParamValue::Append(value), // directly replace the partial value
                                ParamValue::Assign(v) => {
                                    if v.is_array() {
                                        // extend the existing array with the new value
                                        let mut new_value = v.as_array().unwrap().clone();
                                        if let Value::Array(extend_array) = value {
                                            new_value.extend(extend_array);
                                        } else {
                                            unreachable!(); // verified in the head of function
                                        }
                                        ParamValue::Assign(Value::Array(new_value))
                                    } else {
                                        // the previous value assignment already replace the old array by others,
                                        // so the append value is the same as the assign value
                                        ParamValue::Assign(value)
                                    }
                                }
                                ParamValue::Append(v) => {
                                    if v.is_array() {
                                        // extend the existing array with the new value
                                        let mut new_value = v.as_array().unwrap().clone();
                                        if let Value::Array(extend_array) = value {
                                            new_value.extend(extend_array);
                                        } else {
                                            unreachable!(); // verified in the head of function
                                        }
                                        ParamValue::Append(Value::Array(new_value))
                                    } else {
                                        ParamValue::Append(value)
                                    }
                                }
                            };
                            entry.insert(new_value);
                        } else {
                            entry.insert(ParamValue::Assign(value));
                        }
                    }
                }
            }
            ParamValue::Assign(v) | ParamValue::Append(v) => {
                // Patch on the value
                // already inherent the key and arg from the previous loop
                let mut remain_keys = vec![];
                if let Some(next_key) = keys_next {
                    remain_keys.push(next_key);
                }
                remain_keys.extend(keys);

                // continue find the parent in the value
                let mut parent = v;
                for idx in 0..remain_keys.len() {
                    let next_key = remain_keys[idx + 1];
                    match key {
                        SHPartialKeyPart::Name(_) => {
                            if !parent.is_object() {
                                return Err(AzUserUsageError::InvalidArgumentValueError(
                                    // TODO: improve the error message
                                    format!("the patch key is invalid: {}", key),
                                ));
                            }
                            let name = arg.normalized_name_or(key);
                            if !parent.as_object().unwrap().contains_key(&name) {
                                // insert a new object
                                let append_value = if matches!(next_key, SHPartialKeyPart::Index(_))
                                {
                                    Value::Array(Vec::new())
                                } else {
                                    Value::Object(Map::new())
                                };
                                parent
                                    .as_object_mut()
                                    .unwrap()
                                    .insert(name.to_owned(), append_value);
                            }
                            parent = parent.as_object_mut().unwrap().get_mut(&name).unwrap();
                        }
                        SHPartialKeyPart::Index(index) => {
                            if !parent.is_array() {
                                return Err(AzUserUsageError::InvalidArgumentValueError(
                                    // TODO: improve the error message
                                    format!("the patch key is invalid: {}", key),
                                ));
                            }
                            let index = *index as usize;
                            if index >= parent.as_array().unwrap().len() {
                                // index out of range
                                return Err(AzUserUsageError::InvalidArgumentValueError(
                                    // TODO: improve the error message
                                    format!("the patch key is invalid: {}", key),
                                ));
                            }
                            parent = parent.as_array_mut().unwrap().get_mut(index).unwrap();
                        }
                    }

                    key = next_key;
                    arg = arg.retrieve_sub_arg(&[key.to_owned()]).unwrap();
                }

                match key {
                    SHPartialKeyPart::Name(_) => {
                        if !parent.is_object() {
                            return Err(AzUserUsageError::InvalidArgumentValueError(
                                // TODO: improve the error message
                                format!("the patch key is invalid: {}", key),
                            ));
                        }
                        let name = arg.normalized_name_or(key);
                        let new_value = if value_op.is_append {
                            match parent.as_object_mut().unwrap().entry(name.to_owned()) {
                                SerdeEntry::Vacant(_) => value_op.value,
                                SerdeEntry::Occupied(entry) => {
                                    if let Value::Array(array) = entry.get() {
                                        let mut array = array.clone();
                                        if let Value::Array(extend_array) = value_op.value {
                                            array.extend(extend_array);
                                            Value::Array(array)
                                        } else {
                                            unreachable!(); // verified in the head of function
                                        }
                                    } else {
                                        // for other old types, directly replace them
                                        value_op.value
                                    }
                                }
                            }
                        } else {
                            value_op.value
                        };
                        let new_value = arg.normalize_value_names(new_value);
                        parent
                            .as_object_mut()
                            .unwrap()
                            .insert(name.to_owned(), new_value);
                    }
                    SHPartialKeyPart::Index(index) => {
                        if !parent.is_array() {
                            return Err(AzUserUsageError::InvalidArgumentValueError(
                                // TODO: improve the error message
                                format!("the patch key is invalid: {}", key),
                            ));
                        }
                        let index = *index as usize;
                        if index >= parent.as_array().unwrap().len() {
                            // index out of range
                            return Err(AzUserUsageError::InvalidArgumentValueError(
                                // TODO: improve the error message
                                format!("the patch key is invalid: {}", key),
                            ));
                        }
                        let new_value = if value_op.is_append {
                            let v = &parent.as_array_mut().unwrap()[index];
                            if let Value::Array(array) = v {
                                // extend the existing array with the new value
                                let mut array = array.clone();
                                if let Value::Array(extend_array) = value_op.value {
                                    array.extend(extend_array);
                                    Value::Array(array)
                                } else {
                                    unreachable!(); // verified in the head of function
                                }
                            } else {
                                // directly replace the value
                                value_op.value
                            }
                        } else {
                            // directly replace the value
                            value_op.value
                        };
                        let new_value = arg.normalize_value_names(new_value);
                        parent.as_array_mut().unwrap()[index] = new_value;
                    }
                }
            }
        }

        Ok(())
    }

    /// format the params based on the argument definition, error will be returned if the value is invalid
    ///
    /// The format process includes:
    /// 1. apply the default values
    /// 2. check values with format rules
    pub fn format_and_build(self) -> AzResult<CrsParams> {
        let mut crs_params = CrsParams::default();

        let params_map = if let ParamValue::Partial(partial) = self.params {
            partial
        } else {
            unreachable!();
        };

        for (key, value) in params_map.into_iter() {
            let arg = CrsArgWrapper::build_by_keys(
                self.command,
                &SHPartialKeys::from_parts(&[SHPartialKeyPart::Name(key.to_string())]),
            )
            .unwrap();

            if matches!(value, ParamValue::Assign(_)) {
                crs_params.patch_keys.push(arg.prefix_keys().to_string());
            } else if matches!(value, ParamValue::Append(_)) {
                crs_params
                    .patch_keys
                    .push(format!("{}+", arg.prefix_keys()));
            }
            let v = format_param_value(value, &arg, &mut crs_params.patch_keys)?;
            let json_bytes = serde_json::to_vec(&v).unwrap();
            crs_params.params.insert(key, json_bytes);
        }

        // apply default values if argument is not assigned
        for arg in self.command.args.iter() {
            if arg.default.is_none() {
                continue;
            }
            if crs_params.params.contains_key(&arg.name) {
                continue;
            }

            let default: Value = arg.default.as_ref().unwrap().clone().try_into()?;
            let value = ParamValue::Assign(default);
            let key = arg.name.to_string();
            let arg = CrsArgWrapper::new(Some(self.command), arg)?;
            crs_params.patch_keys.push(arg.prefix_keys().to_string());
            let v = format_param_value(value, &arg, &mut crs_params.patch_keys)?;
            let json_bytes = serde_json::to_vec(&v).unwrap();
            crs_params.params.insert(key, json_bytes);
        }

        Ok(crs_params)
    }
}

fn format_param_value(
    value: ParamValue,
    arg: &CrsArgWrapper,
    patch_keys: &mut Vec<String>,
) -> AzResult<Value> {
    let arg_type = arg.unwrapped_arg_type();
    let v = match arg_type {
        crs_arg::ArgType::String(str_arg) => {
            let value = match value {
                ParamValue::Assign(v) => v,
                _ => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(
                        "invalid value type for string argument".to_string(),
                    )
                    .into())
                }
            };

            match str_arg.format.as_ref() {
                None => value,
                Some(crs_string_arg::Format::Binary(fmt)) => format_str_binary(value, fmt)?,
                Some(crs_string_arg::Format::Byte(fmt)) => format_str_byte(value, fmt)?,
                Some(crs_string_arg::Format::String(fmt)) => format_str_string(value, fmt)?,
                Some(crs_string_arg::Format::Duration(fmt)) => format_str_duration(value, fmt)?,
                Some(crs_string_arg::Format::Date(fmt)) => format_str_date(value, fmt)?,
                Some(crs_string_arg::Format::DateTime(fmt)) => format_str_datetime(value, fmt)?,
                Some(crs_string_arg::Format::Time(fmt)) => format_str_time(value, fmt)?,
                Some(crs_string_arg::Format::Uuid(fmt)) => format_str_uuid(value, fmt)?,
                Some(crs_string_arg::Format::Password(fmt)) => format_str_password(value, fmt)?,
                Some(crs_string_arg::Format::SubscriptionId(fmt)) => {
                    format_str_subscription_id(value, fmt)?
                }
                Some(crs_string_arg::Format::ResourceGroupName(fmt)) => {
                    format_str_resource_group_name(value, fmt)?
                }
                Some(crs_string_arg::Format::ResourceId(fmt)) => {
                    format_str_resource_id(value, fmt)?
                }
                Some(crs_string_arg::Format::ResourceLocation(fmt)) => {
                    format_str_resource_location(value, fmt)?
                }
            }
        }
        crs_arg::ArgType::Number(num_arg) => {
            let value = match value {
                ParamValue::Assign(v) => v,
                _ => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(
                        "invalid value type for string argument".to_string(),
                    )
                    .into())
                }
            };
            match num_arg.format.as_ref() {
                None => value,
                Some(crs_number_arg::Format::Int32(fmt)) => format_number_int32(value, fmt)?,
                Some(crs_number_arg::Format::Int64(fmt)) => format_number_int64(value, fmt)?,
                Some(crs_number_arg::Format::Uint32(fmt)) => format_number_uint32(value, fmt)?,
                Some(crs_number_arg::Format::Uint64(fmt)) => format_number_uint64(value, fmt)?,
                Some(crs_number_arg::Format::Float(fmt)) => format_number_float(value, fmt)?,
                Some(crs_number_arg::Format::Double(fmt)) => format_number_double(value, fmt)?,
            }
        }
        crs_arg::ArgType::Boolean(_) => match value {
            ParamValue::Assign(v) => v,
            _ => {
                return Err(AzUserUsageError::InvalidArgumentValueError(
                    "invalid value type for string argument".to_string(),
                )
                .into())
            }
        },
        crs_arg::ArgType::Object(object_arg) => match value {
            ParamValue::Assign(v) => match v {
                Value::Null => v,
                Value::Object(object) => {
                    let mut new_obj = serde_json::Map::with_capacity(object.len());
                    for (key, value) in object.into_iter() {
                        let sub_arg = arg
                            .retrieve_sub_arg(&[SHPartialKeyPart::parse_name(&key).unwrap()])
                            .unwrap_or_else(|_| panic!("missing sub argument {} in object", key));
                        let value =
                            format_param_value(ParamValue::Assign(value), &sub_arg, patch_keys)?;
                        new_obj.insert(key, value);
                    }
                    // apply default value
                    for prop in object_arg.props.iter() {
                        if prop.default.is_none() {
                            continue;
                        }
                        if new_obj.contains_key(&prop.name) {
                            continue;
                        }

                        let default: Value = prop.default.as_ref().unwrap().clone().try_into()?;
                        let value = ParamValue::Assign(default);
                        let key = prop.name.to_string();
                        let sub_arg = arg
                            .retrieve_sub_arg(&[SHPartialKeyPart::parse_name(&key).unwrap()])
                            .unwrap_or_else(|_| panic!("missing sub argument {} in object", key));
                        let value = format_param_value(value, &sub_arg, patch_keys)?;
                        new_obj.insert(key, value);
                    }

                    if let Some(format) = object_arg.format {
                        format_object(Value::Object(new_obj), &format)?
                    } else {
                        Value::Object(new_obj)
                    }
                }
                _ => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(
                        "invalid value type for object argument".to_string(),
                    )
                    .into());
                }
            },
            ParamValue::Partial(partial) => {
                let mut partial_object = serde_json::Map::with_capacity(partial.len());
                for (key, value) in partial.into_iter() {
                    let sub_arg = arg
                        .retrieve_sub_arg(&[SHPartialKeyPart::parse_name(&key).unwrap()])
                        .unwrap();
                    if matches!(value, ParamValue::Assign(_)) {
                        patch_keys.push(sub_arg.prefix_keys().to_string());
                    } else if matches!(value, ParamValue::Append(_)) {
                        patch_keys.push(format!("{}+", sub_arg.prefix_keys()))
                    }
                    let v = format_param_value(value, &sub_arg, patch_keys)?;
                    partial_object.insert(key, v);
                }
                // apply default , for the update command the default value should not defined in the argument definition
                for prop in object_arg.props.iter() {
                    if prop.default.is_none() {
                        continue;
                    }
                    if partial_object.contains_key(&prop.name) {
                        continue;
                    }

                    let default: Value = prop.default.as_ref().unwrap().clone().try_into()?;
                    let value = ParamValue::Assign(default);
                    let key = prop.name.to_string();
                    let sub_arg = arg
                        .retrieve_sub_arg(&[SHPartialKeyPart::parse_name(&key).unwrap()])
                        .unwrap_or_else(|_| panic!("missing sub argument {} in object", key));
                    patch_keys.push(sub_arg.prefix_keys().to_string());
                    let value = format_param_value(value, &sub_arg, patch_keys)?;
                    partial_object.insert(key, value);
                }
                Value::Object(partial_object)
            }
            ParamValue::Append(_) => {
                return Err(AzUserUsageError::InvalidArgumentValueError(
                    "object argument does not support append".to_string(),
                )
                .into())
            }
        },
        crs_arg::ArgType::Array(array_arg) => match value {
            ParamValue::Assign(v) | ParamValue::Append(v) => match v {
                Value::Null => v,
                Value::Array(array) => {
                    let mut new_arr = Vec::with_capacity(array.len());
                    for (idx, item) in array.into_iter().enumerate() {
                        let sub_arg = arg
                            .retrieve_sub_arg(&[SHPartialKeyPart::Index(idx as u16)])
                            .unwrap();
                        let item =
                            format_param_value(ParamValue::Assign(item), &sub_arg, patch_keys)?;
                        new_arr.push(item);
                    }
                    if let Some(format) = array_arg.format {
                        format_array(Value::Array(new_arr), &format)?
                    } else {
                        Value::Array(new_arr)
                    }
                }
                _ => {
                    return Err(AzUserUsageError::InvalidArgumentValueError(
                        "invalid value type for array argument".to_string(),
                    )
                    .into());
                }
            },
            ParamValue::Partial(partial) => {
                let mut partial_array = serde_json::Map::with_capacity(partial.len());
                for (key, value) in partial.into_iter() {
                    let sub_arg = arg
                        .retrieve_sub_arg(&[SHPartialKeyPart::parse_index(
                            &key[1..key.len() - 1], // escape the brackets
                        )
                        .unwrap()])
                        .unwrap();
                    if matches!(value, ParamValue::Assign(_)) {
                        patch_keys.push(sub_arg.prefix_keys().to_string())
                    } else if matches!(value, ParamValue::Append(_)) {
                        patch_keys.push(format!("{}+", sub_arg.prefix_keys()))
                    }
                    let v = format_param_value(value, &sub_arg, patch_keys)?;
                    partial_array.insert(key, v);
                }
                Value::Object(partial_array)
            }
        },
        crs_arg::ArgType::AnyType(_) => match value {
            ParamValue::Assign(v) | ParamValue::Append(v) => v,
            ParamValue::Partial(_) => {
                return Err(AzUserUsageError::InvalidArgumentValueError(
                    "any type argument does not support partial".to_string(),
                )
                .into())
            }
        },
        crs_arg::ArgType::Cls(_) => {
            unreachable!()
        }
    };

    Ok(v)
}

#[cfg(test)]
impl ParamValue {
    fn new_partial(params: Vec<(&str, ParamValue)>) -> ParamValue {
        let mut hashmap = HashMap::new();
        for (key, value) in params {
            hashmap.insert(key.to_string(), value);
        }
        ParamValue::Partial(hashmap)
    }
}

#[cfg(test)]
mod test {
    use serde_json::json;

    use crate::{
        argparse::{parse_arg_values, ArgOp, CrsArgWrapper},
        pb::{
            crs_arg::ArgType, CrsAnyTypeArg, CrsArg, CrsArgDefault, CrsArrayArg, CrsBooleanArg,
            CrsNumberArg, CrsObjectArg, CrsObjectArgAdditionalProperties, CrsStringArg,
        },
    };

    use super::*;

    fn parse_arg<'a>(
        command: &CrsCommand,
        name_or_alias: &str,
        values: impl IntoIterator<Item = &'a str>,
    ) -> AzResult<Option<Vec<ArgOp>>> {
        let mut matched_arg = None;
        for arg in command.args.iter() {
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

        let wrapper = CrsArgWrapper::new(Some(command), matched_arg)?;

        let ops = parse_arg_values(&wrapper, values)?;
        Ok(Some(ops))
    }

    #[test]
    fn test_apply_builder() {
        let mut command = CrsCommand::default();

        // object arg
        let object_arg = CrsArg {
            var_name: "$Instance.object".to_string(),
            name: "object".to_string(),
            options: vec!["object-alias".to_string()],
            nullable: true,
            arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                props: vec![
                    CrsArg {
                        var_name: "$Instance.object.str".to_string(),
                        name: "str".to_string(),
                        options: vec!["str-alias".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::String(CrsStringArg::default())),
                        ..Default::default()
                    },
                    CrsArg {
                        var_name: "$Instance.object.num".to_string(),
                        name: "num".to_string(),
                        options: vec!["num".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::Number(CrsNumberArg::default())),
                        ..Default::default()
                    },
                    CrsArg {
                        var_name: "$Instance.object.bool".to_string(),
                        name: "bool".to_string(),
                        options: vec!["bool".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::Boolean(CrsBooleanArg::default())),
                        ..Default::default()
                    },
                    CrsArg {
                        var_name: "$Instance.object.array".to_string(),
                        name: "array".to_string(),
                        options: vec!["array".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::Array(Box::new(CrsArrayArg {
                            item: Some(Box::new(CrsArg {
                                var_name: "$Instance.object.array[]".to_string(),
                                nullable: true,
                                arg_type: Some(ArgType::String(CrsStringArg::default())),
                                ..Default::default()
                            })),
                            ..Default::default()
                        }))),
                        ..Default::default()
                    },
                    CrsArg {
                        var_name: "$Instance.object.tags".to_string(),
                        name: "tags".to_string(),
                        options: vec!["tags".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                            additional_props: Some(Box::new(CrsObjectArgAdditionalProperties {
                                item: Some(Box::new(CrsArg {
                                    var_name: "$Instance.object.tags{}".to_string(),
                                    nullable: true,
                                    arg_type: Some(ArgType::String(CrsStringArg::default())),
                                    ..Default::default()
                                })),
                                ..Default::default()
                            })),
                            ..Default::default()
                        }))),
                        ..Default::default()
                    },
                ],
                additional_props: Some(Box::new(CrsObjectArgAdditionalProperties {
                    item: Some(Box::new(CrsArg {
                        var_name: "$Instance.object{}".to_string(),
                        nullable: true,
                        arg_type: Some(ArgType::AnyType(CrsAnyTypeArg::default())),
                        ..Default::default()
                    })),
                    ..Default::default()
                })),
                ..Default::default()
            }))),
            ..Default::default()
        };
        command.args.push(object_arg);
        // create object without additional properties
        let object_arg = CrsArg {
            var_name: "$Instance.object2".to_string(),
            name: "object2".to_string(),
            options: vec!["object2".to_string()],
            arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                props: vec![
                    CrsArg {
                        var_name: "$Instance.object2.str".to_string(),
                        name: "str".to_string(),
                        options: vec!["str".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::String(CrsStringArg::default())),
                        ..Default::default()
                    },
                    CrsArg {
                        var_name: "$Instance.object2.propObj".to_string(),
                        name: "propObj".to_string(),
                        options: vec!["prop-obj".to_string()],
                        nullable: true,
                        arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                            props: vec![CrsArg {
                                var_name: "$Instance.object2.propObj.nameAlias".to_string(),
                                name: "nameAlias".to_string(),
                                options: vec!["name-alias".to_string()],
                                nullable: true,
                                arg_type: Some(ArgType::String(CrsStringArg::default())),
                                ..Default::default()
                            }],
                            additional_props: Some(Box::new(CrsObjectArgAdditionalProperties {
                                item: Some(Box::new(CrsArg {
                                    var_name: "$Instance.object2.propObj{}".to_string(),
                                    nullable: true,
                                    arg_type: Some(ArgType::Array(Box::new(CrsArrayArg {
                                        item: Some(Box::new(CrsArg {
                                            var_name: "$Instance.object2.propObj{}[]".to_string(),
                                            nullable: true,
                                            arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                                                props: vec![CrsArg {
                                                    var_name: "$Instance.object2.propObj{}[].nameAlias".to_string(),
                                                    name: "nameAlias".to_string(),
                                                    options: vec!["name-alias".to_string()],
                                                    nullable: true,
                                                    arg_type: Some(ArgType::String(CrsStringArg::default())),
                                                    ..Default::default()
                                                }],
                                                ..Default::default()
                                            }))),
                                            ..Default::default()
                                        })),
                                        ..Default::default()
                                    }))),
                                    ..Default::default()
                                })),
                                ..Default::default()
                            })),
                            ..Default::default()
                        }))),
                        ..Default::default()
                    },
                ],
                ..Default::default()
            }))),
            ..Default::default()
        };
        command.args.push(object_arg);

        // create a standalone array arg
        let array_arg = CrsArg {
            var_name: "$Instance.arr".to_string(),
            name: "arr".to_string(),
            options: vec!["arr".to_string()],
            nullable: true,
            arg_type: Some(ArgType::Array(Box::new(CrsArrayArg {
                item: Some(Box::new(CrsArg {
                    var_name: "$Instance.arr[]".to_string(),
                    nullable: true,
                    arg_type: Some(ArgType::String(CrsStringArg::default())),
                    ..Default::default()
                })),
                ..Default::default()
            }))),
            ..Default::default()
        };
        command.args.push(array_arg);

        let mut builder = CrsParamBuilder::new(&command);

        for (arg, values, expected) in [
            ("arr", vec!["[5]=f"], 
            ParamValue::new_partial(
                vec![("arr", ParamValue::new_partial(vec![
                    ("[5]", ParamValue::Assign(Value::String("f".to_string())))
                ]))]
            )),
            ("arr", vec!["+=[1,2,3]"], 
            ParamValue::new_partial(
                vec![("arr", ParamValue::Append(Value::Array(json!(["1", "2", "3"]).as_array().unwrap().clone())))]
            )),
            ("arr", vec!["+=[4,5]"],
            ParamValue::new_partial(
                vec![("arr", ParamValue::Append(Value::Array(json!(["1", "2", "3", "4", "5"]).as_array().unwrap().clone())))]
            )),
            ("arr", vec!["[a,b,c]"],
            ParamValue::new_partial(vec![("arr", ParamValue::Assign(json!(["a", "b", "c"])))])),
            ("arr", vec!["+=[a,b,c]"],
            ParamValue::new_partial(vec![("arr", ParamValue::Assign(json!(["a", "b", "c", "a", "b", "c"])))])),
            ("arr", vec!["null"],
            ParamValue::new_partial(
                vec![("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object", vec!["array[5]=f", "tags.a=x"],
            ParamValue::new_partial(
                vec![("object", ParamValue::new_partial(vec![
                    ("array", ParamValue::new_partial(vec![
                        ("[5]", ParamValue::Assign(Value::String("f".to_string())))
                    ])),
                    ("tags", ParamValue::new_partial(vec![
                        ("a", ParamValue::Assign(Value::String("x".to_string())))
                    ]))
                ])), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object", vec!["array+=[a,b]"],
            ParamValue::new_partial(
                vec![("object", ParamValue::new_partial(vec![
                    ("array", ParamValue::Append(Value::Array(json!(["a", "b"]).as_array().unwrap().clone()))),
                    ("tags", ParamValue::new_partial(vec![
                        ("a", ParamValue::Assign(Value::String("x".to_string())))
                    ]))
                ])), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object-alias", vec!["str-alias=abc", "tags={a:b,c:1,d:false,e:true,f:null}", "additional1=1", "array=[c,d]"],
            ParamValue::new_partial(
                vec![("object", ParamValue::new_partial(vec![
                    ("str", ParamValue::Assign(Value::String("abc".to_string()))),
                    ("tags", ParamValue::Assign(Value::Object(json!({"a": "b", "c": "1", "d": "false", "e": "true", "f": null}).as_object()
                    .unwrap()
                    .clone()))),
                    ("additional1", ParamValue::Assign(Value::Number(json!(1).as_number().unwrap().clone()))),
                    ("array", ParamValue::Assign(Value::Array(json!(["c", "d"]).as_array().unwrap().clone())))
                ])), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object-alias", vec!["additional1+=[1,2,3]"],  // because previous value is assign, so the append value will be converted to assign value
            ParamValue::new_partial(
                vec![("object", ParamValue::new_partial(vec![
                    ("str", ParamValue::Assign(Value::String("abc".to_string()))),
                    ("tags", ParamValue::Assign(Value::Object(json!({"a": "b", "c": "1", "d": "false", "e": "true", "f": null}).as_object()
                    .unwrap()
                    .clone()))),
                    ("additional1", ParamValue::Assign(Value::Array(json!([1, 2, 3]).as_array().unwrap().clone()))),
                    ("array", ParamValue::Assign(Value::Array(json!(["c", "d"]).as_array().unwrap().clone())))
                ])), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object-alias", vec!["array+=[a,b]"],
            ParamValue::new_partial(
                vec![("object", ParamValue::new_partial(vec![
                    ("str", ParamValue::Assign(Value::String("abc".to_string()))),
                    ("tags", ParamValue::Assign(Value::Object(json!({"a": "b", "c": "1", "d": "false", "e": "true", "f": null}).as_object()
                    .unwrap()
                    .clone()))),
                    ("additional1", ParamValue::Assign(Value::Array(json!([1, 2, 3]).as_array().unwrap().clone()))),
                    ("array", ParamValue::Assign(Value::Array(json!(["c", "d", "a", "b"]).as_array().unwrap().clone())))
                ])), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object-alias", vec!["array[3]=f", "tags.c=2", "tags.g=g", "additional1={a:[1,2],b:[{c:d},{e:f}]}"],
            ParamValue::new_partial(
                vec![("object", ParamValue::new_partial(vec![
                    ("str", ParamValue::Assign(Value::String("abc".to_string()))),
                    ("tags", ParamValue::Assign(Value::Object(json!({"a": "b", "c": "2", "d": "false", "e": "true", "f": null, "g": "g"}).as_object()
                    .unwrap()
                    .clone()))),
                    ("additional1", ParamValue::Assign(Value::Object(json!({"a": [1,2], "b": [{"c": "d"}, {"e": "f"}]}).as_object()
                    .unwrap()
                    .clone()))),
                    ("array", ParamValue::Assign(Value::Array(json!(["c", "d", "a", "f"]).as_array().unwrap().clone())))
                ])), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object-alias", vec!["{str-alias:abc,num:123,bool:true,array:[1,b,false,true,null],tags:{a:b,c:1,d:false,e:true,f:null},additional1:1,additional2:false,additional3:[1,b,false,true,null],additional4:{a:b,c:1,d:false,e:true,f:null},additional5:[{a:b,c:d}]}"],                
                ParamValue::new_partial(vec![("object", ParamValue::Assign(Value::Object(
                    json!({
                        "str": "abc",
                        "num": 123,
                        "bool": true,
                        "array": ["1", "b", "false", "true", null],  // as the element of array is string so number and boolean are converted to string
                        "tags": {"a": "b", "c": "1", "d": "false", "e": "true", "f": null},  // as the item of dict is string so number and boolean are converted to string
                        "additional1": 1,
                        "additional2": false,
                        "additional3": [1, "b", false, true, null],
                        "additional4": {"a": "b", "c": 1, "d": false, "e": true, "f": null}, // as the item additional is any type so number and boolean are kept as number and boolean
                        "additional5": [{"a": "b", "c": "d"}]
                    })
                    .as_object()
                    .unwrap()
                    .clone())
                )), ("arr", ParamValue::Assign(Value::Null))])
            ),
            ("object-alias",vec!["null"],
            ParamValue::new_partial(
                vec![("object", ParamValue::Assign(Value::Null)), ("arr", ParamValue::Assign(Value::Null))]
            )),
            ("object2", vec!["str=ccc", "prop-obj.name-alias=xxx","prop-obj.add-1[0].name-alias=222"],
            ParamValue::new_partial(
                vec![("object2", ParamValue::new_partial(vec![
                    ("str", ParamValue::Assign(Value::String("ccc".to_string()))),
                    ("propObj", ParamValue::new_partial(vec![
                        ("nameAlias", ParamValue::Assign(Value::String("xxx".to_string()))),
                        ("add-1", ParamValue::new_partial(vec![
                            ("[0]", ParamValue::new_partial(vec![
                                ("nameAlias", ParamValue::Assign(Value::String("222".to_string())))
                            ]))
                        ]))
                    ]))
                ])),
                ("arr", ParamValue::Assign(Value::Null)),
                ("object", ParamValue::Assign(Value::Null))]
            )),
            ("object2", vec!["{str:abc,prop-obj:{name-alias:def,add-1:[{name-alias:add-1-1}]}}"],
            ParamValue::new_partial(
                vec![("object2", ParamValue::Assign(Value::Object(json!({"str": "abc", "propObj": {"nameAlias": "def", "add-1": [{"nameAlias": "add-1-1"}]}}).as_object()
                .unwrap()
                .clone()))),
                ("arr", ParamValue::Assign(Value::Null)),
                ("object", ParamValue::Assign(Value::Null))]
            ))
        ] {
            let ops = parse_arg(&command, arg, values).unwrap().unwrap().into_iter();
            for op in ops {
                if let ArgOp::Value(op) = op {
                    builder.apply(op).unwrap();
                } else {
                    panic!("no value op");
                }
            }
            assert_eq!(builder.params, expected);
        }
    }

    #[test]
    fn test_format_and_build() {
        let mut command = CrsCommand::default();

        // object arg
        let mut object_arg = CrsArg::default();
        object_arg.var_name = "$Instance.object".to_string();
        object_arg.name = "object".to_string();
        object_arg.options = vec!["object".to_string()];
        object_arg.nullable = true;
        let object_type = {
            let mut object_type = CrsObjectArg::default();
            // default string property
            let mut porp_str = CrsArg::default();
            porp_str.name = "prop_df".to_string();
            porp_str.options = vec!["prop_df".to_string()];
            porp_str.nullable = true;
            porp_str.arg_type = Some(ArgType::String(CrsStringArg::default()));
            porp_str.default = Some(CrsArgDefault {
                value: "\"default_str\"".to_string(),
            });
            object_type.props.push(porp_str);
            // internal object
            let mut prop_obj = CrsArg::default();
            prop_obj.name = "prop_obj".to_string();
            prop_obj.options = vec!["prop_obj".to_string()];
            prop_obj.nullable = true;
            let prop_obj_type = {
                let mut object_type = CrsObjectArg::default();
                // define the string property type
                let mut str_arg = CrsArg::default();
                str_arg.name = "str".to_string();
                str_arg.options = vec!["str".to_string()];
                str_arg.nullable = true;
                str_arg.arg_type = Some(ArgType::String(CrsStringArg::default()));
                object_type.props.push(str_arg);
                // define the number property type
                let mut num_arg = CrsArg::default();
                num_arg.name = "num".to_string();
                num_arg.options = vec!["num".to_string()];
                num_arg.nullable = true;
                num_arg.arg_type = Some(ArgType::Number(CrsNumberArg::default()));
                object_type.props.push(num_arg);
                // define the bool property type
                let mut bool_arg = CrsArg::default();
                bool_arg.name = "bool".to_string();
                bool_arg.options = vec!["bool".to_string()];
                bool_arg.nullable = true;
                bool_arg.arg_type = Some(ArgType::Boolean(CrsBooleanArg::default()));
                object_type.props.push(bool_arg);
                // define the array property type
                let mut array_arg = CrsArg::default();
                array_arg.name = "array".to_string();
                array_arg.options = vec!["array".to_string()];
                array_arg.nullable = true;
                let mut array_type = CrsArrayArg::default();
                let mut array_item_arg = CrsArg::default();
                array_item_arg.nullable = true;
                let array_item_type = {
                    let mut object_type = CrsObjectArg::default();
                    let mut str_arg = CrsArg::default();
                    str_arg.name = "str".to_string();
                    str_arg.options = vec!["str".to_string()];
                    str_arg.nullable = true;
                    str_arg.arg_type = Some(ArgType::String(CrsStringArg::default()));
                    object_type.props.push(str_arg);
                    let mut num_arg = CrsArg::default();
                    num_arg.name = "default_num".to_string();
                    num_arg.options = vec!["default_num".to_string()];
                    num_arg.nullable = true;
                    num_arg.arg_type = Some(ArgType::Number(CrsNumberArg::default()));
                    num_arg.default = Some(CrsArgDefault {
                        value: "1".to_string(),
                    });
                    object_type.props.push(num_arg);
                    object_type
                };
                array_item_arg.arg_type = Some(ArgType::Object(Box::new(array_item_type)));
                array_type.item = Some(Box::new(array_item_arg));
                array_arg.arg_type = Some(ArgType::Array(Box::new(array_type)));
                object_type.props.push(array_arg);
                object_type
            };
            prop_obj.arg_type = Some(ArgType::Object(Box::new(prop_obj_type)));
            object_type.props.push(prop_obj);

            object_type
        };

        // define the object type
        object_arg.arg_type = Some(ArgType::Object(Box::new(object_type)));
        command.args.push(object_arg);

        // define the default arg
        let mut default_arg = CrsArg::default();
        default_arg.name = "default".to_string();
        default_arg.options = vec!["default".to_string()];
        default_arg.nullable = true;
        default_arg.arg_type = Some(ArgType::String(CrsStringArg::default()));
        default_arg.default = Some(CrsArgDefault {
            value: "\"default_arg\"".to_string(),
        });
        command.args.push(default_arg);

        for (arg, values, mut expected, mut patch_keys) in [
            (
                "object",
                vec!["prop_obj.str=abc", "prop_obj.array[2].str=bcd"],
                json!({
                    "object": {
                        "prop_df": "default_str",
                        "prop_obj": {
                            "str": "abc",
                            "array": {"[2]": {"str": "bcd", "default_num": 1}}
                        }
                    },
                    "default": "default_arg"
                })
                .as_object()
                .unwrap()
                .clone(),
                vec![
                    "object.prop_df",
                    "object.prop_obj.str",
                    "object.prop_obj.array[2].str",
                    "object.prop_obj.array[2].default_num",
                    "default",
                ],
            ),
            (
                "object",
                vec!["{prop_obj:{str:abc,array:[{str:bcd}]}}"],
                json!({
                    "object": {
                        "prop_df": "default_str",
                        "prop_obj": {
                            "str": "abc",
                            "array": [{"str": "bcd", "default_num": 1}]
                        }
                    },
                    "default": "default_arg"
                })
                .as_object()
                .unwrap()
                .clone(),
                vec!["object", "default"],
            ),
            (
                "object",
                vec!["prop_obj.array+=[{str:bcd}]"],
                json!({
                    "object": {
                        "prop_df": "default_str",
                        "prop_obj": {
                            "array": [{"str": "bcd", "default_num": 1}]
                        }
                    },
                    "default": "default_arg"
                })
                .as_object()
                .unwrap()
                .clone(),
                vec!["object.prop_df", "object.prop_obj.array+", "default"],
            ),
        ] {
            let mut builder = CrsParamBuilder::new(&command);
            let ops = parse_arg(&command, arg, values)
                .unwrap()
                .unwrap()
                .into_iter();
            for op in ops {
                if let ArgOp::Value(op) = op {
                    builder.apply(op).unwrap();
                } else {
                    panic!("no value op");
                }
            }
            let params = builder.format_and_build().unwrap();
            for (key, v) in params.params.iter() {
                let v = serde_json::from_slice::<Value>(v).unwrap();
                assert_eq!(v, expected.remove(key).unwrap());
            }
            assert!(expected.is_empty());

            for key in params.patch_keys {
                if let Some(index) = patch_keys.iter().position(|k| k.eq(&key)) {
                    patch_keys.remove(index);
                } else {
                    panic!("key {} not found in patch_keys", key);
                }
            }
            assert!(patch_keys.is_empty());
        }
    }
}
