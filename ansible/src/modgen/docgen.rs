use std::collections::HashMap;

use az_core::pb::{CrsArg, CrsCommand};
use serde::{Deserialize, Serialize};

/// See: https://docs.ansible.com/ansible/latest/dev_guide/developing_modules_documenting.html#documentation-block
#[derive(Debug, Clone, Serialize, Deserialize)]
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

#[derive(Debug, Clone, Serialize, Deserialize)]
struct Deprecation {
    removed_in: String,
    removed_by_date: String,
    why: String,
    alternative: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
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
    #[serde(skip_serializing_if = "Option::is_none")]
    suboptions: Option<HashMap<String, Opt>>,
    // Additional fields that's less interesting
    // requirements
    // seealso
    // attributes
    // notes
}

/// See: https://docs.ansible.com/ansible/latest/dev_guide/developing_program_flow_modules.html#argument-spec
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
enum Type {
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

impl Document {
    pub fn from_cmd(cmd: &CrsCommand) -> Self {
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
                    let opt = Opt::from_cmd_arg(arg);
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

    fn from_cmd_arg(arg: &CrsArg) -> Self {
        let mut choices: Option<Vec<String>> = None;
        let mut typ = Type::Unknown;
        let mut elements = None;
        let mut suboptions = None;
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
                        .map(|p| (p.name.clone(), Opt::from_cmd_arg(p)))
                        .collect();
                    suboptions = Some(m);
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
