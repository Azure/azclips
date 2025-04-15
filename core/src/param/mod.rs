use std::collections::{hash_map::Entry, HashMap};

use serde_json::{map::Entry as SerdeEntry, Map, Value};

use crate::{
    argparse::{partial::SHPartialKeyPart, ArgValueAssignOp},
    error::AzUserUsageError,
    pb::command::{self, CmdCommand},
    AzResult,
};

#[derive(Debug, PartialEq, Eq)]
enum ParamValue {
    Assign(Value),
    Append(Value),
    Partial(HashMap<String, ParamValue>),
}

pub(crate) struct CmdParamBuilder {
    // the binary parameters will be stored as a file path in the value and converted to a binary data when building the command
    params: ParamValue,
}

impl Default for CmdParamBuilder {
    fn default() -> Self {
        Self {
            params: ParamValue::Partial(HashMap::new()),
        }
    }
}

impl CmdParamBuilder {
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
        let mut keys_next = keys.next();

        while let Some(next_key) = keys_next {
            match parent {
                ParamValue::Partial(partial) => {
                    parent = partial
                        .entry(key.to_string())
                        .or_insert(ParamValue::Partial(HashMap::new()));
                    key = next_key;
                    keys_next = keys.next();
                }
                _ => {
                    break;
                }
            }
        }

        match parent {
            ParamValue::Partial(partial) => match partial.entry(key.to_string()) {
                Entry::Vacant(entry) => {
                    if value_op.is_append {
                        entry.insert(ParamValue::Append(value_op.value));
                    } else {
                        entry.insert(ParamValue::Assign(value_op.value));
                    }
                }
                Entry::Occupied(mut entry) => {
                    if value_op.is_append {
                        let new_value = match entry.get() {
                            ParamValue::Partial(_) => ParamValue::Append(value_op.value), // directly replace the partial value
                            ParamValue::Assign(v) => {
                                if v.is_array() {
                                    // extend the existing array with the new value
                                    let mut new_value = v.as_array().unwrap().clone();
                                    if let Value::Array(extend_array) = value_op.value {
                                        new_value.extend(extend_array.into_iter());
                                    } else {
                                        unreachable!(); // verified in the head of function
                                    }
                                    ParamValue::Assign(Value::Array(new_value))
                                } else {
                                    // the previous value assignment already replace the old array by others,
                                    // so the append value is the same as the assign value
                                    ParamValue::Assign(value_op.value)
                                }
                            }
                            ParamValue::Append(v) => {
                                if v.is_array() {
                                    // extend the existing array with the new value
                                    let mut new_value = v.as_array().unwrap().clone();
                                    if let Value::Array(extend_array) = value_op.value {
                                        new_value.extend(extend_array.into_iter());
                                    } else {
                                        unreachable!(); // verified in the head of function
                                    }
                                    ParamValue::Append(Value::Array(new_value))
                                } else {
                                    ParamValue::Append(value_op.value)
                                }
                            }
                        };
                        entry.insert(new_value);
                    } else {
                        entry.insert(ParamValue::Assign(value_op.value));
                    }
                }
            },
            ParamValue::Assign(v) | ParamValue::Append(v) => {
                // Patch on the value
                let mut remain_keys = vec![key];
                if let Some(next_key) = keys_next {
                    remain_keys.push(next_key);
                }
                remain_keys.extend(keys);

                // continue find the parent in the value
                let mut parent = v;
                for idx in 0..remain_keys.len() - 1 {
                    let key = remain_keys[idx];
                    let next_key = remain_keys[idx + 1];
                    match key {
                        SHPartialKeyPart::Name(name) => {
                            if !parent.is_object() {
                                return Err(AzUserUsageError::InvalidArgumentValueError(
                                    // TODO: improve the error message
                                    format!("the patch key is invalid: {}", key),
                                ));
                            }
                            if !parent.as_object().unwrap().contains_key(name) {
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
                            parent = parent.as_object_mut().unwrap().get_mut(name).unwrap();
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
                }

                let key = remain_keys[remain_keys.len() - 1];
                match key {
                    SHPartialKeyPart::Name(name) => {
                        if !parent.is_object() {
                            return Err(AzUserUsageError::InvalidArgumentValueError(
                                // TODO: improve the error message
                                format!("the patch key is invalid: {}", key),
                            ));
                        }
                        let new_value = if value_op.is_append {
                            match parent.as_object_mut().unwrap().entry(name.to_owned()) {
                                SerdeEntry::Vacant(_) => value_op.value,
                                SerdeEntry::Occupied(entry) => {
                                    if let Value::Array(array) = entry.get() {
                                        let mut array = array.clone();
                                        if let Value::Array(extend_array) = value_op.value {
                                            array.extend(extend_array.into_iter());
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
                                    array.extend(extend_array.into_iter());
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
    /// 3. parse the file path as binary data if the argument is a binary type string
    pub fn format_and_build(mut self, command: &CmdCommand) -> AzResult<command::CmdParams> {
        // let mut cmd_params = command::CmdParams::default();
        // let params_map = self.params.as_object_mut().unwrap();
        // for (key, value) in params_map.iter_mut() {
        //     // TODO: format value based on the command argument definition, the serde_json::to_vec should be done in the format function
        //     let v = serde_json::to_vec(value).unwrap();

        //     cmd_params.params.insert(key.to_string(), v);
        // }

        // for patch_key in self.patch_keys {
        //     let mut patch_key_str = String::new();
        //     for (idx, part) in patch_key.iter().enumerate() {
        //         match part {
        //             PatchKeyPart::Partial(SHPartialKeyPart::Name(_)) => {
        //                 if idx > 0 {
        //                     patch_key_str.push_str(".");
        //                 }
        //             }
        //             _ => {}
        //         }
        //         patch_key_str.push_str(&part.to_string());
        //     }
        //     cmd_params.patch_keys.push(patch_key_str);
        // }
        // Ok(cmd_params)
        todo!()
    }
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
        argparse::{arg::CmdArgWrapper, parse_arg_values, ArgOp},
        pb::command::{
            cmd_arg::ArgType, CmdAnyTypeArg, CmdArg, CmdArrayArg, CmdBooleanArg, CmdNumberArg,
            CmdObjectArg, CmdObjectArgAdditionalProperties, CmdStringArg,
        },
    };

    use super::*;

    fn parse_cmd_arg<'a>(
        cmd: &command::CmdCommand,
        name_or_alias: &str,
        values: impl IntoIterator<Item = &'a str>,
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

        let wrapper = CmdArgWrapper::new(Some(cmd), matched_arg)?;

        let ops = parse_arg_values(&wrapper, values)?;
        Ok(Some(ops))
    }

    #[test]
    fn test_apply_builder() {
        let mut builder = CmdParamBuilder::default();

        let mut cmd = CmdCommand::default();

        // object arg
        let mut object_arg = CmdArg::default();
        object_arg.var_name = "$Instance.object".to_string();
        object_arg.name = "object".to_string();
        object_arg.options = vec!["object".to_string()];
        object_arg.nullable = true;
        let mut object_type = CmdObjectArg::default();
        // define the string property type
        let mut str_arg = CmdArg::default();
        str_arg.name = "str".to_string();
        str_arg.options = vec!["str".to_string()];
        str_arg.nullable = true;
        str_arg.arg_type = Some(ArgType::Str(CmdStringArg::default()));
        object_type.props.push(str_arg);
        // define the number property type
        let mut num_arg = CmdArg::default();
        num_arg.name = "num".to_string();
        num_arg.options = vec!["num".to_string()];
        num_arg.nullable = true;
        num_arg.arg_type = Some(ArgType::Number(CmdNumberArg::default()));
        object_type.props.push(num_arg);
        // define the bool property type
        let mut bool_arg = CmdArg::default();
        bool_arg.name = "bool".to_string();
        bool_arg.options = vec!["bool".to_string()];
        bool_arg.nullable = true;
        bool_arg.arg_type = Some(ArgType::Boolean(CmdBooleanArg::default()));
        object_type.props.push(bool_arg);
        // define the array property type
        let mut array_arg = CmdArg::default();
        array_arg.name = "array".to_string();
        array_arg.options = vec!["array".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(ArgType::Str(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(ArgType::Array(Box::new(array_type)));
        object_type.props.push(array_arg);
        // define the dict property type
        let mut dict_arg = CmdArg::default();
        dict_arg.name = "tags".to_string();
        dict_arg.options = vec!["tags".to_string()];
        dict_arg.nullable = true;
        let mut dict_type = CmdObjectArg::default();
        let mut tag_arg = CmdArg::default();
        tag_arg.nullable = true;
        tag_arg.arg_type = Some(ArgType::Str(CmdStringArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(tag_arg)),
        }));
        dict_arg.arg_type = Some(ArgType::Object(Box::new(dict_type)));
        object_type.props.push(dict_arg);
        // define the additional properties
        let mut additional_props = CmdArg::default();
        additional_props.nullable = true;
        additional_props.arg_type = Some(ArgType::AnyType(CmdAnyTypeArg::default()));
        object_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(additional_props)),
        }));
        object_arg.arg_type = Some(ArgType::Object(Box::new(object_type)));
        cmd.args.push(object_arg);
        // create object without additional properties
        let mut object_arg = CmdArg::default();
        object_arg.var_name = "$Instance.object2".to_string();
        object_arg.name = "object2".to_string();
        object_arg.options = vec!["object2".to_string()];
        let mut object_type = CmdObjectArg::default();
        // define the string property type
        let mut str_arg = CmdArg::default();
        str_arg.name = "str".to_string();
        str_arg.options = vec!["str".to_string()];
        str_arg.nullable = true;
        str_arg.arg_type = Some(ArgType::Str(CmdStringArg::default()));
        object_type.props.push(str_arg);
        object_arg.arg_type = Some(ArgType::Object(Box::new(object_type)));
        cmd.args.push(object_arg);

        // create a standalone array arg
        let mut array_arg = CmdArg::default();
        array_arg.name = "arr".to_string();
        array_arg.options = vec!["arr".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(ArgType::Str(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(ArgType::Array(Box::new(array_type)));
        cmd.args.push(array_arg);

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
            ("object", vec!["str=abc", "tags={a:b,c:1,d:false,e:true,f:null}", "additional1=1", "array=[c,d]"],
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
            ("object", vec!["additional1+=[1,2,3]"],  // because previous value is assign, so the append value will be converted to assign value
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
            ("object", vec!["array+=[a,b]"],
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
            ("object", vec!["array[3]=f", "tags.c=2", "tags.g=g", "additional1={a:[1,2],b:[{c:d},{e:f}]}"],
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
            ("object", vec!["{str:abc,num:123,bool:true,array:[1,b,false,true,null],tags:{a:b,c:1,d:false,e:true,f:null},additional1:1,additional2:false,additional3:[1,b,false,true,null],additional4:{a:b,c:1,d:false,e:true,f:null},additional5:[{a:b,c:d}]}"],                
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
        ] {
            let mut ops = parse_cmd_arg(&cmd, arg, values).unwrap().unwrap().into_iter();
            while let Some(op) = ops.next() {
                if let ArgOp::Value(op) = op {
                    builder.apply(op).unwrap();
                } else {
                    panic!("no value op");
                }
            }
            assert_eq!(builder.params, expected);
        }
    }
}
