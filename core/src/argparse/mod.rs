pub mod arg;
mod partial;
mod shorthand;

use arg::CmdArgWrapper;
use serde_json::Value;

use crate::error::{AzResult, AzUserUsageError};
use crate::pb::command;

use self::partial::SHPartialKeys;
use self::shorthand::{SHResult, SHORTHAND_SYNTAX_PARSER};

#[derive(Debug, PartialEq)]
pub enum ArgOp {
    PromptInput(ArgPromptInputOp),
    ValueAssign(ArgValueAssignOp),
    Help(ArgHelpOp),
}

#[derive(Debug, PartialEq)]
pub struct ArgPromptInputOp {
    keys: SHPartialKeys,
    prompt: crate::pb::command::CmdArgPromptInput,
}

#[derive(Debug, PartialEq)]
pub struct ArgValueAssignOp {
    keys: SHPartialKeys,
    /// whether the value is filled from the blank value or not
    from_blank: bool,
    value: Value,
}

#[derive(Debug, PartialEq)]
pub struct ArgHelpOp {
    keys: SHPartialKeys,
}

/// convert the values into the argument operations, return None when the argument is not found
pub fn parse_cmd_arg(
    cmd: &command::CmdCommand,
    name_or_alias: &str,
    values: &[&str],
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

    let arg = CmdArgWrapper::new(cmd, matched_arg)?;
    let ops = parse_arg_values(&arg, values)?;

    Ok(Some(ops))
}

fn parse_arg_values(arg: &CmdArgWrapper, values: &[&str]) -> AzResult<Vec<ArgOp>> {
    match arg.unwrapped_arg_type {
        command::cmd_arg::ArgType::Str(_)
        | command::cmd_arg::ArgType::Number(_)
        | command::cmd_arg::ArgType::Boolean(_) => parse_simple_arg(arg, values),

        command::cmd_arg::ArgType::AnyType(_) => parse_any_type_arg(arg, values),

        command::cmd_arg::ArgType::Object(_) | command::cmd_arg::ArgType::Array(_) => {
            parse_composite_arg(arg, values)
        }

        command::cmd_arg::ArgType::Cls(_) => {
            unreachable!()
        }
    }
}

fn parse_simple_arg(arg: &CmdArgWrapper, values: &[&str]) -> AzResult<Vec<ArgOp>> {
    if values.len() > 1 {
        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "Argument '{}' can't accept multiple values",
            arg
        ))
        .into());
    }

    let value = values.first().map(|v| *v);
    let op = parse_arg_value_str(arg, value)?;
    Ok(vec![op])
}

fn parse_any_type_arg(arg: &CmdArgWrapper, values: &[&str]) -> AzResult<Vec<ArgOp>> {
    if values.len() > 1 {
        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "Argument '{}' can't accept multiple values",
            arg
        ))
        .into());
    }

    if values.is_empty() {
        let op = parse_arg_value_str(arg, None)?;
        return Ok(vec![op]);
    }

    let value = values[0];
    if SHORTHAND_SYNTAX_PARSER.split_partial_value(value).is_some() {
        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "Argument '{}' is any type arg which only support full value shorthand syntax, please don't use partial value shorthand syntax. If it's a simple string, please wrap it with single quotes.",
            arg,
        ))
        .into());
    }

    let op = parse_arg_value_str(arg, Some(value))?;
    Ok(vec![op])
}

fn parse_composite_arg(arg: &CmdArgWrapper, values: &[&str]) -> AzResult<Vec<ArgOp>> {
    if values.is_empty() {
        let op = parse_arg_value_str(arg, None)?;
        return Ok(vec![op]);
    }

    let mut ops = Vec::with_capacity(values.len());
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

fn parse_arg_value_str(
    arg: &CmdArgWrapper,
    value: Option<&str>, // None for the blank input
) -> AzResult<ArgOp> {
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
            .to_owned()
            .try_into()?;
        let value = arg.verify_value(blank)?;
        return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
            keys: arg.prefix_keys.to_owned(),
            from_blank: true,
            value,
        }));
    }

    let value = value.unwrap();

    match arg.unwrapped_arg_type {
        command::cmd_arg::ArgType::AnyType(_)
        | command::cmd_arg::ArgType::Array(_)
        | command::cmd_arg::ArgType::Object(_) => {
            // try parse from json string
            if let Ok(v) = serde_json::from_str::<Value>(value) {
                let value = arg.verify_value(v)?;
                return Ok(ArgOp::ValueAssign(ArgValueAssignOp {
                    keys: arg.prefix_keys.to_owned(),
                    from_blank: false,
                    value,
                }));
            }
        }
        _ => {}
    }

    match SHORTHAND_SYNTAX_PARSER.parse(value, Some(arg.to_owned()))? {
        SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp { keys })),
        SHResult::Value(value) => Ok(ArgOp::ValueAssign(ArgValueAssignOp {
            keys: arg.prefix_keys.to_owned(),
            from_blank: false,
            value,
        })),
    }
}

#[cfg(test)]
mod tests {
    use crate::{
        argparse::partial::SHPartialKeyPart,
        error::AzError,
        pb::command::{
            cmd_arg::ArgType, cmd_number_arg, cmd_string_arg, CmdAnyTypeArg, CmdArg, CmdArrayArg,
            CmdBooleanArg, CmdCommand, CmdInt64Format, CmdNumberArg, CmdObjectArg,
            CmdObjectArgAdditionalProperties, CmdStringArg, CmdStringFormat,
        },
    };
    use serde_json::json;

    use super::*;

    #[test]
    fn test_parse_cmd_simple_arg() {
        let mut cmd = CmdCommand::default();
        // string arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Path.name".to_string();
        str_arg.name = "name".to_string();
        str_arg.options = vec!["name".to_string()];
        str_arg.blank = Some(command::CmdArgBlank {
            value: "\"autoname\"".to_string(),
        });
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(ArgType::Str(str_type));
        cmd.args.push(str_arg);

        // prompt input arg
        let mut prompt_input_arg = CmdArg::default();
        prompt_input_arg.var_name = "$Path.secret".to_string();
        prompt_input_arg.name = "secret".to_string();
        prompt_input_arg.options = vec!["secret".to_string()];
        prompt_input_arg.prompt = Some(command::CmdArgPromptInput {
            prompt: "Enter your secret".to_string(),
            secret: true,
            confirm: true,
        });
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        prompt_input_arg.arg_type = Some(ArgType::Str(str_type));
        cmd.args.push(prompt_input_arg);

        // size arg
        let mut size_arg = CmdArg::default();
        size_arg.var_name = "$Path.size".to_string();
        size_arg.name = "size".to_string();
        size_arg.options = vec!["size".to_string()];
        let mut number_type = CmdNumberArg::default();
        number_type.format = Some(cmd_number_arg::Format::Int64(CmdInt64Format::default()));
        size_arg.arg_type = Some(ArgType::Number(number_type));
        cmd.args.push(size_arg);

        // bool arg
        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Path.enabled".to_string();
        bool_arg.name = "enabled".to_string();
        bool_arg.options = vec!["enabled".to_string()];
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);

        // test blank value
        let ops = parse_cmd_arg(&cmd, "name", &vec![]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("name".to_string())],
                },
                from_blank: true,
                value: Value::String("autoname".to_string()),
            })
        );

        // test normal value
        let ops = parse_cmd_arg(&cmd, "name", &vec!["hello world"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("name".to_string())],
                },
                from_blank: false,
                value: Value::String("hello world".to_string()),
            })
        );

        let ops = parse_cmd_arg(&cmd, "name", &vec!["'hello world'"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("name".to_string())],
                },
                from_blank: false,
                value: Value::String("hello world".to_string()),
            })
        );

        let ops = parse_cmd_arg(&cmd, "name", &vec!["\"hello world\""])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("name".to_string())],
                },
                from_blank: false,
                value: Value::String("\"hello world\"".to_string()),
            })
        );

        let ops = parse_cmd_arg(&cmd, "name", &vec!["''/hello world'/'"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("name".to_string())],
                },
                from_blank: false,
                value: Value::String("'hello world'".to_string()),
            })
        );

        // test null value
        let ops = parse_cmd_arg(&cmd, "name", &vec!["null"]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("name".to_string())],
                },
                from_blank: false,
                value: Value::Null,
            })
        );

        // test secret prompt input
        let ops = parse_cmd_arg(&cmd, "secret", &vec![]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::PromptInput(ArgPromptInputOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("secret".to_string())],
                },
                prompt: command::CmdArgPromptInput {
                    prompt: "Enter your secret".to_string(),
                    secret: true,
                    confirm: true,
                },
            })
        );

        // test size arg
        let ops = parse_cmd_arg(&cmd, "size", &vec!["1024"]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("size".to_string())],
                },
                from_blank: false,
                value: Value::Number(serde_json::Number::from(1024)),
            })
        );

        // test bool arg
        let ops = parse_cmd_arg(&cmd, "enabled", &vec![]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("enabled".to_string())],
                },
                from_blank: true,
                value: Value::Bool(true),
            })
        );

        let ops = parse_cmd_arg(&cmd, "enabled", &vec!["true"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("enabled".to_string())],
                },
                from_blank: false,
                value: Value::Bool(true),
            })
        );

        let ops = parse_cmd_arg(&cmd, "enabled", &vec!["no"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("enabled".to_string())],
                },
                from_blank: false,
                value: Value::Bool(false),
            })
        );

        // test argument not found
        let result = parse_cmd_arg(&cmd, "not_found", &vec![]).unwrap();
        assert_eq!(result, None);

        // test error cases
        let result = parse_cmd_arg(&cmd, "name", &vec!["hello", "world"]);
        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidArgumentValueError(msg))) => {
                assert_eq!(msg, "Argument 'name' can't accept multiple values");
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }

        let result = parse_cmd_arg(&cmd, "size", &vec![]);
        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidArgumentValueError(msg))) => {
                assert_eq!(
                    msg,
                    "Argument 'size' requires a value but no value is provided"
                );
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }
    }

    #[test]
    fn test_parse_cmd_composite_arg() {
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

        let ops = parse_cmd_arg(&cmd, "object", &vec![
            "{str:abc,num:123,bool:true,array:[1,b,false,true,null],tags:{a:b,c:1,d:false,e:true,f:null},additional1:1,additional2:false,additional3:[1,b,false,true,null],additional4:{a:b,c:1,d:false,e:true,f:null},additional5:[{a:b,c:d}]}"
        ]).unwrap().unwrap();

        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("object".to_string())]
                },
                from_blank: false,
                value: Value::Object(
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
                    .clone()
                ),
            })
        );

        // test the json string
        let ops = parse_cmd_arg(&cmd, "object", &vec![
            "{\"str\": \"abc\", \"num\": 123, \"bool\": true, \"array\": [\"1\", \"b\", \"false\", \"true\", null], \"tags\": {\"a\": \"b\", \"c\": \"1\", \"d\": \"false\", \"e\": \"true\", \"f\":null}, \"additional1\":1, \"additional2\":false, \"additional3\":[1, \"b\", false, true, null], \"additional4\":{\"a\": \"b\", \"c\":1, \"d\":false, \"e\":true, \"f\":null}, \"additional5\":[{\"a\": \"b\", \"c\": \"d\"}]}"
        ]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![SHPartialKeyPart::Name("object".to_string())]
                },
                from_blank: false,
                value: Value::Object(
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
                    .clone()
                ),
            })
        );

        // test partial value assignment
        let ops = parse_cmd_arg(
            &cmd,
            "object",
            &vec![
                "array[0]=1",
                "str=abc",
                "additional3=[1,b,false,true,null]",
                "tags.e=true",
                "tags.f=null",
            ],
        )
        .unwrap()
        .unwrap();
        assert_eq!(ops.len(), 5);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![
                        SHPartialKeyPart::Name("object".to_string()),
                        SHPartialKeyPart::Name("array".to_string()),
                        SHPartialKeyPart::Index(0)
                    ]
                },
                from_blank: false,
                value: Value::String("1".to_string()),
            })
        );
        assert_eq!(
            ops[1],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![
                        SHPartialKeyPart::Name("object".to_string()),
                        SHPartialKeyPart::Name("str".to_string())
                    ]
                },
                from_blank: false,
                value: Value::String("abc".to_string()),
            })
        );
        assert_eq!(
            ops[2],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![
                        SHPartialKeyPart::Name("object".to_string()),
                        SHPartialKeyPart::Name("additional3".to_string())
                    ]
                },
                from_blank: false,
                value: Value::Array(
                    json!([1, "b", false, true, null])
                        .as_array()
                        .unwrap()
                        .clone()
                ),
            })
        );
        assert_eq!(
            ops[3],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![
                        SHPartialKeyPart::Name("object".to_string()),
                        SHPartialKeyPart::Name("tags".to_string()),
                        SHPartialKeyPart::Name("e".to_string())
                    ]
                },
                from_blank: false,
                value: Value::String("true".to_string()),
            })
        );
        assert_eq!(
            ops[4],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![
                        SHPartialKeyPart::Name("object".to_string()),
                        SHPartialKeyPart::Name("tags".to_string()),
                        SHPartialKeyPart::Name("f".to_string())
                    ]
                },
                from_blank: false,
                value: Value::Null,
            })
        );

        // test partial value with json
        let ops = parse_cmd_arg(
            &cmd,
            "object",
            &vec!["additional3=[1, \"b\", false, \"true\", null, {\"a\": \"b\", \"c\": \"d\"}]"],
        )
        .unwrap()
        .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::ValueAssign(ArgValueAssignOp {
                keys: SHPartialKeys {
                    parts: vec![
                        SHPartialKeyPart::Name("object".to_string()),
                        SHPartialKeyPart::Name("additional3".to_string())
                    ]
                },
                from_blank: false,
                value: Value::Array(
                    json!([1, "b", false, "true", null, {"a": "b", "c": "d"}])
                        .as_array()
                        .unwrap()
                        .clone()
                ),
            })
        );

        // TODO: test error cases
    }
}
