use std::collections::HashMap;

use az_core::pb::{CrsArg, CrsCommand};
use serde::{Deserialize, Serialize};

/// See: https://docs.ansible.com/ansible/latest/dev_guide/developing_modules_documenting.html#documentation-block
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
pub struct Document {
    module: String,
    short_description: String,
    description: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    version_added: Option<String>,
    author: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    deprecated: Option<Deprecation>,
    #[serde(skip_serializing_if = "HashMap::is_empty")]
    options: HashMap<String, Opt>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
struct Deprecation {
    removed_in: String,
    removed_by_date: String,
    why: String,
    alternative: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
struct Opt {
    #[serde(skip)]
    name: String,
    description: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    required: Option<bool>,
    // default value here is the string representation of the value
    #[serde(skip_serializing_if = "Option::is_none")]
    default: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    choices: Option<Vec<String>>,
    #[serde(rename = "type")]
    typ: Type,
    #[serde(skip_serializing_if = "Option::is_none")]
    elements: Option<Type>,
    #[serde(skip_serializing_if = "Option::is_none")]
    aliases: Option<Vec<String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    version_added: Option<String>,
    #[serde(skip_serializing_if = "HashMap::is_empty")]
    suboptions: HashMap<String, Opt>,
    // Additional fields that's less interesting
    // requirements
    // seealso
    // attributes
    // notes
}

/// See: https://docs.ansible.com/ansible/latest/dev_guide/developing_program_flow_modules.html#argument-spec
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Default)]
#[serde(rename_all = "lowercase")]
enum Type {
    #[default]
    Unknown,
    Str,
    List,
    Dict,
    Bool,
    Int,
    Float,
    Path,
    Raw,
    JsonArg,
    Json,
    Bytes,
    Bits,
}

impl From<&CrsCommand> for Document {
    fn from(cmd: &CrsCommand) -> Self {
        let help = cmd.help.clone().unwrap_or_default();
        Self {
            module: cmd.name.clone(),
            short_description: help.short,
            description: help.long.unwrap_or_default(),
            version_added: None,
            author: "azure".to_string(),
            deprecated: None,
            options: cmd
                .args
                .iter()
                .map(|arg| {
                    let opt = Opt::from(arg);
                    (opt.name.clone(), opt)
                })
                .collect(),
        }
    }
}

impl Opt {
    fn arg_type(arg_type: &az_core::pb::crs_arg::ArgType) -> Type {
        let typ: Type;
        match arg_type {
            az_core::pb::crs_arg::ArgType::String(_) => {
                typ = Type::Str;
            }
            az_core::pb::crs_arg::ArgType::Number(v) => match v.format {
                None => {
                    typ = Type::Int;
                }
                Some(v) => match v {
                    az_core::pb::crs_number_arg::Format::Float(_) => typ = Type::Float,
                    az_core::pb::crs_number_arg::Format::Double(_) => typ = Type::Float,
                    _ => typ = Type::Int,
                },
            },
            az_core::pb::crs_arg::ArgType::Boolean(_) => {
                typ = Type::Bool;
            }
            az_core::pb::crs_arg::ArgType::Object(_) => {
                // We don't really need to differentiate a map of string (additionalProperties only), or a object in the
                // context of swagger.
                typ = Type::Dict;
            }
            az_core::pb::crs_arg::ArgType::Array(_) => {
                typ = Type::List;
            }
            az_core::pb::crs_arg::ArgType::AnyType(_) => {
                // TODO(magodo): check the correct type
                typ = Type::Raw;
            }
            az_core::pb::crs_arg::ArgType::Cls(_) => {
                // TODO(magodo): check the correct type
                typ = Type::Raw;
            }
        }
        typ
    }
}

impl From<&CrsArg> for Opt {
    fn from(arg: &CrsArg) -> Self {
        let mut choices: Option<Vec<String>> = None;
        let mut typ = Type::Unknown;
        let mut elements = None;
        let mut suboptions = HashMap::new();
        if let Some(ref at) = arg.arg_type {
            typ = Self::arg_type(at);
            match at {
                az_core::pb::crs_arg::ArgType::String(v) => {
                    if let Some(ref e) = v.r#enum {
                        choices = Some(e.items.iter().map(|v| v.value.clone()).collect());
                    }
                }
                az_core::pb::crs_arg::ArgType::Number(v) => {
                    if let Some(ref e) = v.r#enum {
                        choices = Some(e.items.iter().map(|v| v.value.clone()).collect());
                    }
                }
                az_core::pb::crs_arg::ArgType::Boolean(_) => {}
                az_core::pb::crs_arg::ArgType::Object(v) => {
                    let m: HashMap<String, Opt> = v
                        .props
                        .iter()
                        .map(|p| (p.name.clone(), Opt::from(p)))
                        .collect();
                    suboptions = m;
                }
                az_core::pb::crs_arg::ArgType::Array(v) => {
                    if let Some(ref vv) = v.item {
                        if let Some(ref t) = vv.arg_type {
                            elements = Some(Self::arg_type(t))
                        }
                    }
                }
                az_core::pb::crs_arg::ArgType::AnyType(_) => {}
                az_core::pb::crs_arg::ArgType::Cls(_) => {}
            }
        }
        Self {
            name: arg.name.clone(),
            description: arg.help.clone().unwrap_or_default().short,
            required: if arg.required { Some(true) } else { None },
            default: match arg.default.clone() {
                Some(v) => Some(v.value),
                None => None,
            },
            choices,
            typ,
            elements,
            aliases: None,
            version_added: None,
            suboptions,
        }
    }
}

#[cfg(test)]
mod test {

    use super::*;
    use az_core::pb::crs_number_arg::Format;
    use az_core::pb::{crs_arg::*, *};

    #[test]
    fn test_opt_arg_type() {
        struct TestCase {
            input: ArgType,
            expect: Type,
        }

        let cases = vec![
            TestCase {
                input: ArgType::Cls(CrsClsArg::default()),
                expect: Type::Raw,
            },
            TestCase {
                input: ArgType::String(CrsStringArg::default()),
                expect: Type::Str,
            },
            TestCase {
                input: ArgType::Number(CrsNumberArg {
                    r#enum: None,
                    format: Some(Format::Int32(CrsInt32Format::default())),
                }),
                expect: Type::Int,
            },
            TestCase {
                input: ArgType::Number(CrsNumberArg {
                    r#enum: None,
                    format: Some(Format::Float(CrsFloatFormat::default())),
                }),
                expect: Type::Float,
            },
            TestCase {
                input: ArgType::Boolean(CrsBooleanArg::default()),
                expect: Type::Bool,
            },
            TestCase {
                input: ArgType::Object(Box::default()),
                expect: Type::Dict,
            },
            TestCase {
                input: ArgType::Array(Box::default()),
                expect: Type::List,
            },
            TestCase {
                input: ArgType::AnyType(CrsAnyTypeArg {}),
                expect: Type::Raw,
            },
        ];

        for case in cases {
            assert_eq!(Opt::arg_type(&case.input), case.expect);
        }
    }

    #[test]
    fn test_opt_from() {
        struct TestCase {
            input: CrsArg,
            expect: Opt,
        }

        let default_arg = CrsArg::default();
        let default_opt = Opt::default();

        let cases = vec![
            // String
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::String(CrsStringArg::default())),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Str,
                    ..default_opt.clone()
                },
            },
            // String with enum
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::String(CrsStringArg {
                        r#enum: Some(CrsArgEnum {
                            items: vec![
                                CrsArgEnumItem {
                                    name: "foo".into(),
                                    value: "foo".into(),
                                    internal: false,
                                },
                                CrsArgEnumItem {
                                    name: "bar".into(),
                                    value: "bar".into(),
                                    internal: false,
                                },
                            ],
                            support_extension: false,
                            case_sensitive: true,
                        }),
                        format: None,
                    })),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Str,
                    choices: Some(vec!["foo".into(), "bar".into()]),
                    ..default_opt.clone()
                },
            },
            // Number
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Number(CrsNumberArg::default())),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Int,
                    ..default_opt.clone()
                },
            },
            // Number - Float with enum
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Number(CrsNumberArg {
                        r#enum: Some(CrsArgEnum {
                            items: vec![
                                CrsArgEnumItem {
                                    name: "1".into(),
                                    value: "1".into(),
                                    internal: false,
                                },
                                CrsArgEnumItem {
                                    name: "2".into(),
                                    value: "2".into(),
                                    internal: false,
                                },
                            ],
                            support_extension: false,
                            case_sensitive: true,
                        }),
                        format: Some(Format::Float(CrsFloatFormat::default())),
                    })),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Float,
                    choices: Some(vec!["1".into(), "2".into()]),
                    ..default_opt.clone()
                },
            },
            // Boolean
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Boolean(CrsBooleanArg::default())),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Bool,
                    ..default_opt.clone()
                },
            },
            // Array of bool
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Array(Box::new(CrsArrayArg {
                        cls_type: None,
                        item: Some(Box::new(CrsArg {
                            arg_type: Some(ArgType::Boolean(CrsBooleanArg::default())),
                            ..default_arg.clone()
                        })),
                        format: None,
                    }))),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::List,
                    elements: Some(Type::Bool),
                    ..default_opt.clone()
                },
            },
            // AnyType
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::AnyType(CrsAnyTypeArg {})),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Raw,
                    ..default_opt.clone()
                },
            },
            // Cls
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Cls(CrsClsArg::default())),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Raw,
                    ..default_opt.clone()
                },
            },
            // Object of no args
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                        cls_type: None,
                        ps_flatten: false,
                        format: None,
                        props: vec![],
                        additional_props: None,
                    }))),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Dict,
                    ..default_opt.clone()
                },
            },
            // Object of primitive args
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                        cls_type: None,
                        ps_flatten: false,
                        format: None,
                        props: vec![CrsArg {
                            arg_type: Some(ArgType::Boolean(CrsBooleanArg::default())),
                            ..default_arg.clone()
                        }],
                        additional_props: None,
                    }))),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Dict,
                    suboptions: [(
                        "".into(),
                        Opt {
                            typ: Type::Bool,
                            ..default_opt.clone()
                        },
                    )]
                    .into_iter()
                    .collect(),
                    ..default_opt.clone()
                },
            },
            // Object of nested object of primitive args
            TestCase {
                input: CrsArg {
                    arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                        cls_type: None,
                        ps_flatten: false,
                        format: None,
                        props: vec![CrsArg {
                            arg_type: Some(ArgType::Object(Box::new(CrsObjectArg {
                                cls_type: None,
                                ps_flatten: false,
                                format: None,
                                props: vec![CrsArg {
                                    arg_type: Some(ArgType::Boolean(CrsBooleanArg::default())),
                                    ..default_arg.clone()
                                }],
                                additional_props: None,
                            }))),
                            ..default_arg.clone()
                        }],
                        additional_props: None,
                    }))),
                    ..default_arg.clone()
                },
                expect: Opt {
                    typ: Type::Dict,
                    suboptions: [(
                        "".into(),
                        Opt {
                            typ: Type::Dict,
                            suboptions: [(
                                "".into(),
                                Opt {
                                    typ: Type::Bool,
                                    ..default_opt.clone()
                                },
                            )]
                            .into_iter()
                            .collect(),
                            ..default_opt.clone()
                        },
                    )]
                    .into_iter()
                    .collect(),
                    ..default_opt.clone()
                },
            },
        ];

        for case in cases {
            assert_eq!(Opt::from(&case.input), case.expect);
        }
    }
}
