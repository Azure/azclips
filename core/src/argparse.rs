mod arg;
mod op;
mod partial;
mod shorthand;

use crate::{
    error::{AzResult, AzUserUsageError},
    pb::cmd_arg,
};
use serde_json::Value;
use shorthand::{SHResult, SHORTHAND_SYNTAX_PARSER};

pub use arg::CmdArgWrapper;
pub use op::{ArgHelpOp, ArgOp, ArgPromptInputOp, ArgValueAssignOp};
pub use partial::{SHPartialKeyPart, SHPartialKeys};

/// parse the argument values into the argument operations
pub fn parse_arg_values<'a>(
    arg: &CmdArgWrapper,
    values: impl IntoIterator<Item = &'a str>,
) -> AzResult<Vec<ArgOp>> {
    match arg.unwrapped_arg_type() {
        cmd_arg::ArgType::String(_)
        | cmd_arg::ArgType::Number(_)
        | cmd_arg::ArgType::Boolean(_) => parse_simple_arg(arg, values),

        cmd_arg::ArgType::AnyType(_) => parse_any_type_arg(arg, values),

        cmd_arg::ArgType::Object(_) | cmd_arg::ArgType::Array(_) => {
            parse_composite_arg(arg, values)
        }

        cmd_arg::ArgType::Cls(_) => {
            unreachable!()
        }
    }
}

fn parse_simple_arg<'a>(
    arg: &CmdArgWrapper,
    values: impl IntoIterator<Item = &'a str>,
) -> AzResult<Vec<ArgOp>> {
    let mut values = values.into_iter();
    let value = values.next();
    let op = parse_arg_value_str(arg, value)?;
    if values.next().is_some() {
        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "Argument '{}' can't accept multiple values",
            arg
        ))
        .into());
    }
    Ok(vec![op])
}

fn parse_any_type_arg<'a>(
    arg: &CmdArgWrapper,
    values: impl IntoIterator<Item = &'a str>,
) -> AzResult<Vec<ArgOp>> {
    let mut values = values.into_iter();
    let value = values.next();
    if values.next().is_some() {
        return Err(AzUserUsageError::InvalidArgumentValueError(format!(
            "Argument '{}' can't accept multiple values",
            arg
        ))
        .into());
    }

    if value.is_none() {
        let op = parse_arg_value_str(arg, None)?;
        return Ok(vec![op]);
    }

    let value = value.unwrap();
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

fn parse_composite_arg<'a>(
    arg: &CmdArgWrapper,
    values: impl IntoIterator<Item = &'a str>,
) -> AzResult<Vec<ArgOp>> {
    let mut values = values.into_iter();
    let mut next_value = values.next();
    if next_value.is_none() {
        let op = parse_arg_value_str(arg, None)?;
        return Ok(vec![op]);
    }

    let mut ops = Vec::new();
    while let Some(value) = next_value {
        if let Some((key, value)) = SHORTHAND_SYNTAX_PARSER.split_partial_value(value) {
            let (keys, is_append) = SHORTHAND_SYNTAX_PARSER.parse_partial_value_key_parts(key)?;
            let sub_arg = arg.retrieve_sub_arg(keys.parts())?;
            let mut op = parse_arg_value_str(&sub_arg, value)?;
            op.with_append_flag(is_append);
            ops.push(op);
        } else {
            let op = parse_arg_value_str(arg, Some(value))?;
            ops.push(op);
        }
        next_value = values.next();
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
            return Ok(ArgOp::Prompt(ArgPromptInputOp {
                keys: arg.prefix_keys().to_owned(),
                prompt: prompt.to_owned(),
                is_append: false,
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
        return Ok(ArgOp::Value(ArgValueAssignOp {
            keys: arg.prefix_keys().to_owned(),
            from_blank: true,
            value,
            is_append: false,
        }));
    }

    let value = value.unwrap();

    match arg.unwrapped_arg_type() {
        cmd_arg::ArgType::AnyType(_) | cmd_arg::ArgType::Array(_) | cmd_arg::ArgType::Object(_) => {
            // try parse from json string
            if let Ok(v) = serde_json::from_str::<Value>(value) {
                match arg.verify_value(v) {
                    Ok(v) => {
                        return Ok(ArgOp::Value(ArgValueAssignOp {
                            keys: arg.prefix_keys().to_owned(),
                            from_blank: false,
                            value: v,
                            is_append: false,
                        }));
                    }
                    Err(e) => {
                        // try parse from shorthand syntax, if still error, return the verify error
                        // this if is for the case that a string can be valid for both json and shorthand syntax
                        // for example:
                        // [1,2,3] is valid for both json and shorthand syntax
                        // while element is string type, it will be failed to pass arg.verify_value
                        // but while it's parsed from shorthand syntax, the value `1` will be parsed as string based on the argument type.
                        if SHORTHAND_SYNTAX_PARSER
                            .parse(value, Some(arg.to_owned()))
                            .is_err()
                        {
                            return Err(e);
                        }
                    }
                }
            }
        }
        _ => {}
    }

    match SHORTHAND_SYNTAX_PARSER.parse(value, Some(arg.to_owned()))? {
        SHResult::Help(keys) => Ok(ArgOp::Help(ArgHelpOp { keys })),
        SHResult::Value(value) => Ok(ArgOp::Value(ArgValueAssignOp {
            keys: arg.prefix_keys().to_owned(),
            from_blank: false,
            value,
            is_append: false,
        })),
    }
}

#[cfg(test)]
mod tests {
    use std::io::Write;

    use crate::{
        argparse::partial::{SHPartialKeyPart, SHPartialKeys},
        error::AzError,
        pb::{
            cmd_arg, cmd_number_arg, cmd_string_arg, CmdAnyTypeArg, CmdArg, CmdArgBlank,
            CmdArgPromptInput, CmdArrayArg, CmdBooleanArg, CmdCommand, CmdInt64Format,
            CmdNumberArg, CmdObjectArg, CmdObjectArgAdditionalProperties, CmdStringArg,
            CmdStringFormat,
        },
    };
    use serde_json::json;
    use tempfile::NamedTempFile;

    use super::*;

    fn parse_cmd_arg<'a>(
        cmd: &CmdCommand,
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
    fn test_parse_cmd_simple_arg() {
        let mut cmd = CmdCommand::default();
        // string arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Path.name".to_string();
        str_arg.name = "name".to_string();
        str_arg.options = vec!["name".to_string()];
        str_arg.blank = Some(CmdArgBlank {
            value: "\"autoname\"".to_string(),
        });
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::String(str_type));
        cmd.args.push(str_arg);

        // prompt input arg
        let mut prompt_input_arg = CmdArg::default();
        prompt_input_arg.var_name = "$Path.secret".to_string();
        prompt_input_arg.name = "secret".to_string();
        prompt_input_arg.options = vec!["secret".to_string()];
        prompt_input_arg.prompt = Some(CmdArgPromptInput {
            prompt: "Enter your secret".to_string(),
            secret: true,
            confirm: true,
        });
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        prompt_input_arg.arg_type = Some(cmd_arg::ArgType::String(str_type));
        cmd.args.push(prompt_input_arg);

        // size arg
        let mut size_arg = CmdArg::default();
        size_arg.var_name = "$Path.size".to_string();
        size_arg.name = "size".to_string();
        size_arg.options = vec!["size".to_string()];
        let mut number_type = CmdNumberArg::default();
        number_type.format = Some(cmd_number_arg::Format::Int64(CmdInt64Format::default()));
        size_arg.arg_type = Some(cmd_arg::ArgType::Number(number_type));
        cmd.args.push(size_arg);

        // bool arg
        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Path.enabled".to_string();
        bool_arg.name = "enabled".to_string();
        bool_arg.options = vec!["enabled".to_string()];
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);

        for (name, expression, expected) in vec![
            ("name", vec![], Value::String("autoname".to_string())),
            (
                "name",
                vec!["hello world"],
                Value::String("hello world".to_string()),
            ),
            (
                "name",
                vec!["'hello world'"],
                Value::String("hello world".to_string()),
            ),
            (
                "name",
                vec!["\"hello world\""],
                Value::String("\"hello world\"".to_string()),
            ),
            (
                "name",
                vec!["''/hello world'/'"],
                Value::String("'hello world'".to_string()),
            ),
            ("name", vec!["null"], Value::Null),
            ("name", vec!["true"], Value::String("true".to_string())),
            ("name", vec!["false"], Value::String("false".to_string())),
            ("name", vec!["no"], Value::String("no".to_string())),
            ("name", vec!["1024"], Value::String("1024".to_string())),
            (
                "secret",
                vec!["secret"],
                Value::String("secret".to_string()),
            ),
            (
                "size",
                vec!["1024"],
                Value::Number(serde_json::Number::from(1024)),
            ),
            ("enabled", vec![], Value::Bool(true)),
            ("enabled", vec!["true"], Value::Bool(true)),
            ("enabled", vec!["false"], Value::Bool(false)),
            ("enabled", vec!["no"], Value::Bool(false)),
        ] {
            let ops = parse_cmd_arg(&cmd, name, expression.clone())
                .unwrap()
                .unwrap();
            assert_eq!(ops.len(), 1);
            assert_eq!(
                ops[0],
                ArgOp::Value(ArgValueAssignOp {
                    keys: SHPartialKeys::new([SHPartialKeyPart::Name(name.to_string())]),
                    from_blank: expression.is_empty(),
                    value: expected,
                    is_append: false,
                })
            );
        }

        // test secret prompt input
        let ops = parse_cmd_arg(&cmd, "secret", vec![]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Prompt(ArgPromptInputOp {
                keys: SHPartialKeys::new([SHPartialKeyPart::Name("secret".to_string())]),
                prompt: CmdArgPromptInput {
                    prompt: "Enter your secret".to_string(),
                    secret: true,
                    confirm: true,
                },
                is_append: false,
            })
        );

        // test argument not found
        let result = parse_cmd_arg(&cmd, "not_found", vec![]).unwrap();
        assert_eq!(result, None);

        // test error cases
        let result = parse_cmd_arg(&cmd, "name", vec!["hello", "world"]);
        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidArgumentValueError(msg))) => {
                assert_eq!(msg, "Argument 'name' can't accept multiple values");
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }

        let result = parse_cmd_arg(&cmd, "size", vec![]);
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
    fn test_parse_cmd_simple_arg_by_file() {
        let mut cmd = CmdCommand::default();
        // string arg
        let mut str_arg = CmdArg::default();
        str_arg.var_name = "$Path.name".to_string();
        str_arg.name = "name".to_string();
        str_arg.options = vec!["name".to_string()];
        str_arg.blank = Some(CmdArgBlank {
            value: "\"autoname\"".to_string(),
        });
        str_arg.nullable = true;
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        str_arg.arg_type = Some(cmd_arg::ArgType::String(str_type));
        cmd.args.push(str_arg);

        // prompt input arg
        let mut prompt_input_arg = CmdArg::default();
        prompt_input_arg.var_name = "$Path.secret".to_string();
        prompt_input_arg.name = "secret".to_string();
        prompt_input_arg.options = vec!["secret".to_string()];
        prompt_input_arg.prompt = Some(CmdArgPromptInput {
            prompt: "Enter your secret".to_string(),
            secret: true,
            confirm: true,
        });
        let mut str_type = CmdStringArg::default();
        str_type.format = Some(cmd_string_arg::Format::String(CmdStringFormat::default()));
        prompt_input_arg.arg_type = Some(cmd_arg::ArgType::String(str_type));
        cmd.args.push(prompt_input_arg);

        // size arg
        let mut size_arg = CmdArg::default();
        size_arg.var_name = "$Path.size".to_string();
        size_arg.name = "size".to_string();
        size_arg.options = vec!["size".to_string()];
        let mut number_type = CmdNumberArg::default();
        number_type.format = Some(cmd_number_arg::Format::Int64(CmdInt64Format::default()));
        size_arg.arg_type = Some(cmd_arg::ArgType::Number(number_type));
        cmd.args.push(size_arg);

        // bool arg
        let mut bool_arg = CmdArg::default();
        bool_arg.var_name = "$Path.enabled".to_string();
        bool_arg.name = "enabled".to_string();
        bool_arg.options = vec!["enabled".to_string()];
        let bool_type = CmdBooleanArg::default();
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(bool_type));
        cmd.args.push(bool_arg);

        for (name, value, expected) in vec![
            (
                "name",
                json!("Hello world"),
                Value::String("Hello world".to_string()),
            ),
            (
                "secret",
                json!("secret"),
                Value::String("secret".to_string()),
            ),
            (
                "size",
                json!(1024),
                Value::Number(serde_json::Number::from(1024)),
            ),
            ("enabled", json!(true), Value::Bool(true)),
            ("enabled", json!("no"), Value::Bool(false)),
        ] {
            let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
            writeln!(file, "{}", value).expect("Failed to write to temp file");
            file.flush().expect("Failed to flush temp file");
            let file_value = format!("file://{}", file.path().to_str().unwrap());

            let ops = parse_cmd_arg(&cmd, name, vec![file_value.as_str()])
                .unwrap()
                .unwrap();
            assert_eq!(ops.len(), 1);
            assert_eq!(
                ops[0],
                ArgOp::Value(ArgValueAssignOp {
                    keys: SHPartialKeys::new([SHPartialKeyPart::Name(name.to_string())]),
                    from_blank: false,
                    value: expected,
                    is_append: false,
                })
            );
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
        str_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        object_type.props.push(str_arg);
        // define the number property type
        let mut num_arg = CmdArg::default();
        num_arg.name = "num".to_string();
        num_arg.options = vec!["num".to_string()];
        num_arg.nullable = true;
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(CmdNumberArg::default()));
        object_type.props.push(num_arg);
        // define the bool property type
        let mut bool_arg = CmdArg::default();
        bool_arg.name = "bool".to_string();
        bool_arg.options = vec!["bool".to_string()];
        bool_arg.nullable = true;
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(CmdBooleanArg::default()));
        object_type.props.push(bool_arg);
        // define the array property type
        let mut array_arg = CmdArg::default();
        array_arg.name = "array".to_string();
        array_arg.options = vec!["array".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(array_type)));
        object_type.props.push(array_arg);
        // define the dict property type
        let mut dict_arg = CmdArg::default();
        dict_arg.name = "tags".to_string();
        dict_arg.options = vec!["tags".to_string()];
        dict_arg.nullable = true;
        let mut dict_type = CmdObjectArg::default();
        let mut tag_arg = CmdArg::default();
        tag_arg.nullable = true;
        tag_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(tag_arg)),
        }));
        dict_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(dict_type)));
        object_type.props.push(dict_arg);
        // define the additional properties
        let mut additional_props = CmdArg::default();
        additional_props.nullable = true;
        additional_props.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        object_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(additional_props)),
        }));
        object_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(object_type)));
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
        str_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        object_type.props.push(str_arg);
        object_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(object_type)));
        cmd.args.push(object_arg);

        // create a standalone array arg
        let mut array_arg = CmdArg::default();
        array_arg.name = "arr".to_string();
        array_arg.options = vec!["arr".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(array_type)));
        cmd.args.push(array_arg);

        let ops = parse_cmd_arg(&cmd, "object", vec![
            "{str:abc,num:123,bool:true,array:[1,b,false,true,null],tags:{a:b,c:1,d:false,e:true,f:null},additional1:1,additional2:false,additional3:[1,b,false,true,null],additional4:{a:b,c:1,d:false,e:true,f:null},additional5:[{a:b,c:d}]}"
        ]).unwrap().unwrap();

        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([SHPartialKeyPart::Name("object".to_string())]),
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
                is_append: false,
            })
        );

        // test the json string
        let ops = parse_cmd_arg(&cmd, "object", vec![
            "{\"str\": \"abc\", \"num\": 123, \"bool\": true, \"array\": [\"1\", \"b\", \"false\", \"true\", null], \"tags\": {\"a\": \"b\", \"c\": \"1\", \"d\": \"false\", \"e\": \"true\", \"f\":null}, \"additional1\":1, \"additional2\":false, \"additional3\":[1, \"b\", false, true, null], \"additional4\":{\"a\": \"b\", \"c\":1, \"d\":false, \"e\":true, \"f\":null}, \"additional5\":[{\"a\": \"b\", \"c\": \"d\"}]}"
        ]).unwrap().unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([SHPartialKeyPart::Name("object".to_string())]),
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
                is_append: false,
            })
        );

        // test partial value assignment
        let ops = parse_cmd_arg(
            &cmd,
            "object",
            vec![
                "array[0]=1",
                "str=abc",
                "additional3=[1,b,false,true,null]",
                "tags.e=true",
                "tags.f=null",
                "tags.g='null'",
            ],
        )
        .unwrap()
        .unwrap();
        assert_eq!(ops.len(), 6);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("array".to_string()),
                    SHPartialKeyPart::Index(0)
                ]),
                from_blank: false,
                value: Value::String("1".to_string()),
                is_append: false,
            })
        );
        assert_eq!(
            ops[1],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("str".to_string())
                ]),
                from_blank: false,
                value: Value::String("abc".to_string()),
                is_append: false,
            })
        );
        assert_eq!(
            ops[2],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("additional3".to_string())
                ]),
                from_blank: false,
                value: Value::Array(
                    json!([1, "b", false, true, null])
                        .as_array()
                        .unwrap()
                        .clone()
                ),
                is_append: false,
            })
        );
        assert_eq!(
            ops[3],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("tags".to_string()),
                    SHPartialKeyPart::Name("e".to_string())
                ]),
                from_blank: false,
                value: Value::String("true".to_string()),
                is_append: false,
            })
        );
        assert_eq!(
            ops[4],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("tags".to_string()),
                    SHPartialKeyPart::Name("f".to_string())
                ]),
                from_blank: false,
                value: Value::Null,
                is_append: false,
            })
        );

        assert_eq!(
            ops[5],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("tags".to_string()),
                    SHPartialKeyPart::Name("g".to_string())
                ]),
                from_blank: false,
                value: Value::String("null".to_string()),
                is_append: false,
            })
        );

        // test append value assignment
        let ops = parse_cmd_arg(&cmd, "object", vec!["array+=[2,3,4]"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("array".to_string()),
                ]),
                from_blank: false,
                value: Value::Array(json!(["2", "3", "4"]).as_array().unwrap().clone()),
                is_append: true,
            })
        );

        let ops = parse_cmd_arg(&cmd, "arr", vec!["+=[2,3,4]"])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([SHPartialKeyPart::Name("arr".to_string())]),
                from_blank: false,
                value: Value::Array(json!(["2", "3", "4"]).as_array().unwrap().clone()),
                is_append: true,
            })
        );

        // test partial value with json
        let ops = parse_cmd_arg(
            &cmd,
            "object",
            vec!["additional3=[1, \"b\", false, \"true\", null, {\"a\": \"b\", \"c\": \"d\"}]"],
        )
        .unwrap()
        .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("additional3".to_string())
                ]),
                from_blank: false,
                value: Value::Array(
                    json!([1, "b", false, "true", null, {"a": "b", "c": "d"}])
                        .as_array()
                        .unwrap()
                        .clone()
                ),
                is_append: false,
            })
        );

        // test error cases
        let result = parse_cmd_arg(&cmd, "object", vec!["array[0].attr=abc"]);

        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidArgumentValueError(msg))) => {
                assert_eq!(
                    msg,
                    "'object.array[0]' argument is string type and cannot support internal access by 'attr'"
                );
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }

        let result = parse_cmd_arg(&cmd, "object2", vec!["notexistattr=1"]);

        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidArgumentValueError(msg))) => {
                assert_eq!(
                    msg,
                    "'object2' argument is object type and has no internal property 'notexistattr'"
                );
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }

        let result = parse_cmd_arg(&cmd, "object2", vec!["null"]);

        match result {
            Err(AzError::UserUsage(AzUserUsageError::InvalidArgumentValueError(msg))) => {
                assert_eq!(msg, "'object2' argument is not allowed to be null");
            }
            _ => {
                panic!("Unexpected result: {:?}", result);
            }
        }
    }

    #[test]
    fn test_parse_cmd_composite_arg_by_file() {
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
        str_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        object_type.props.push(str_arg);
        // define the number property type
        let mut num_arg = CmdArg::default();
        num_arg.name = "num".to_string();
        num_arg.options = vec!["num".to_string()];
        num_arg.nullable = true;
        num_arg.arg_type = Some(cmd_arg::ArgType::Number(CmdNumberArg::default()));
        object_type.props.push(num_arg);
        // define the bool property type
        let mut bool_arg = CmdArg::default();
        bool_arg.name = "bool".to_string();
        bool_arg.options = vec!["bool".to_string()];
        bool_arg.nullable = true;
        bool_arg.arg_type = Some(cmd_arg::ArgType::Boolean(CmdBooleanArg::default()));
        object_type.props.push(bool_arg);
        // define the array property type
        let mut array_arg = CmdArg::default();
        array_arg.name = "array".to_string();
        array_arg.options = vec!["array".to_string()];
        array_arg.nullable = true;
        let mut array_type = CmdArrayArg::default();
        let mut array_item_arg = CmdArg::default();
        array_item_arg.nullable = true;
        array_item_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        array_type.item = Some(Box::new(array_item_arg));
        array_arg.arg_type = Some(cmd_arg::ArgType::Array(Box::new(array_type)));
        object_type.props.push(array_arg);
        // define the dict property type
        let mut dict_arg = CmdArg::default();
        dict_arg.name = "tags".to_string();
        dict_arg.options = vec!["tags".to_string()];
        dict_arg.nullable = true;
        let mut dict_type = CmdObjectArg::default();
        let mut tag_arg = CmdArg::default();
        tag_arg.nullable = true;
        tag_arg.arg_type = Some(cmd_arg::ArgType::String(CmdStringArg::default()));
        dict_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(tag_arg)),
        }));
        dict_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(dict_type)));
        object_type.props.push(dict_arg);
        // define the additional properties
        let mut additional_props = CmdArg::default();
        additional_props.nullable = true;
        additional_props.arg_type = Some(cmd_arg::ArgType::AnyType(CmdAnyTypeArg::default()));
        object_type.additional_props = Some(Box::new(CmdObjectArgAdditionalProperties {
            item: Some(Box::new(additional_props)),
        }));
        object_arg.arg_type = Some(cmd_arg::ArgType::Object(Box::new(object_type)));
        cmd.args.push(object_arg);

        let value = json!({
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
        });

        let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
        writeln!(file, "{}", value).expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let file_value = format!("file://{}", file.path().to_str().unwrap());

        let ops = parse_cmd_arg(&cmd, "object", vec![file_value.as_str()])
            .unwrap()
            .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([SHPartialKeyPart::Name("object".to_string())]),
                from_blank: false,
                value: Value::Object(value.as_object().unwrap().clone()),
                is_append: false,
            })
        );

        let value = json!("abc");
        let mut file = NamedTempFile::with_suffix(".json").expect("Failed to create temp file");
        writeln!(file, "{}", value).expect("Failed to write to temp file");
        file.flush().expect("Failed to flush temp file");
        let file_value = format!("file://{}", file.path().to_str().unwrap());

        let ops = parse_cmd_arg(
            &cmd,
            "object",
            vec![format!("tags.x={}", file_value.as_str()).as_str()],
        )
        .unwrap()
        .unwrap();
        assert_eq!(ops.len(), 1);
        assert_eq!(
            ops[0],
            ArgOp::Value(ArgValueAssignOp {
                keys: SHPartialKeys::new([
                    SHPartialKeyPart::Name("object".to_string()),
                    SHPartialKeyPart::Name("tags".to_string()),
                    SHPartialKeyPart::Name("x".to_string())
                ]),
                from_blank: false,
                value: Value::String(value.as_str().unwrap().to_string()),
                is_append: false,
            })
        );
    }
}
