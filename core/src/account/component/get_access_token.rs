use crate::pb::command::CmdHelp;

#[derive(Debug, Default)]
pub(super) struct GetAccessToken {
    name_or_subscription_id: Option<String>,
    resource: Option<String>,
    resource_type: Option<String>,
    scope: Option<String>,
    tenant: Option<String>,
}

impl GetAccessToken {
    pub(super) async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Value>, AzError> {
        println!("get access token: {:?}", self);
        Ok(None)
    }
}

impl GetAccessToken {
    const URI: &'static str = "cmd://azure/account/get-access-token";

    pub fn uri() -> CmdUri {
        CmdUri::from_str(Self::URI).unwrap()
    }

    pub fn get_cmd_interface() -> CmdInterface {
        CmdInterface {
            name: "get-access-token".to_string(),
            help: Some(CmdHelp {
                short: "Get a token for utilities to access Azure.".to_string(),
                long: Some("The token will be valid for at least 5 minutes with the maximum at 60 minutes. If the subscription argument isn't specified, the current account is used.\n\nIn the output, `expires_on` represents a POSIX timestamp and `expiresOn` represents a local datetime. It is recommended for downstream applications to use `expires_on` because it is in UTC.".to_string()),
            }),
            uri: Self::uri().to_string(),
            level: Some(cmd_interface::Level::Command(CmdCommand {
                version: VERSION.to_string(),
                args: Self::args(),
                positional_args: vec![],
            })),
        }
    }

    fn args() -> Vec<CmdArg> {
        vec![
            CmdArg {
                var_name: "nameOrSubscriptionId".to_string(),
                name: "nameOrSubscriptionId".to_string(),
                options: vec![
                    "name".to_string(),
                    "subscription".to_string(),
                    "n".to_string(),
                    "s".to_string(),
                ],
                help: Some(CmdArgHelp {
                    short: "Name or ID of subscription.".to_string(),
                }),
                arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                    format: Some(cmd_string_arg::Format::String(CmdStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
            CmdArg {
                var_name: "resource".to_string(),
                name: "resource".to_string(),
                options: vec!["resource".to_string()],
                help: Some(CmdArgHelp {
                    short: "Azure resource endpoints in Microsoft Entra v1.0.".to_string(),
                }),
                arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                    format: Some(cmd_string_arg::Format::String(CmdStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
            CmdArg {
                var_name: "resourceType".to_string(),
                name: "resourceType".to_string(),
                options: vec!["resource-type".to_string()],
                help: Some(CmdArgHelp {
                    short: "Type of well-known resource.".to_string(),
                }),
                arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                    format: Some(cmd_string_arg::Format::String(CmdStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
            CmdArg {
                var_name: "scope".to_string(),
                name: "scope".to_string(),
                options: vec!["scope".to_string()],
                help: Some(CmdArgHelp {
                    short: "Scope of the resource to access.".to_string(),
                }),
                arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                    format: Some(cmd_string_arg::Format::String(CmdStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
            CmdArg {
                var_name: "tenant".to_string(),
                name: "tenant".to_string(),
                options: vec!["tenant".to_string(), "t".to_string()],
                help: Some(CmdArgHelp {
                    short: "Tenant ID of the resource to access.".to_string(),
                }),
                arg_type: Some(cmd_arg::ArgType::Str(CmdStringArg {
                    format: Some(cmd_string_arg::Format::String(CmdStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
        ]
    }
}
