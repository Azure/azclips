use async_trait::async_trait;
use azure_core::http::ClientOptions;
use serde_json::{json, Value};
use tracing::error;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx}, config::cloud::{endpoint, resource_id}, error::{AzError, AzInternalError}, pb::{crs_arg, crs_string_arg, CrsArg, CrsArgHelp, CrsCommand, CrsHelp, CrsParams, CrsStringArg, CrsStringFormat, CrsSubscriptionIdFormat}, VERSION
};

#[derive(Debug, Default)]
pub struct ShowDemo {
    resource_group_name: String,
    subscription: Option<String>,
}

#[async_trait]
impl AzCommandExecute for ShowDemo {
    type Output = Value;

    async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError> {
        let subscription = match self.subscription.as_ref() {
            Some(s) => s.to_owned(),
            None => ctx.get_default_subscription().await?,
        };
        println!("subscription: {}", subscription);
        println!("resource_group_name: {}", self.resource_group_name);

        let cfg = ctx.get_cfg().await;
        let cloud = cfg.clouds.get_active();
        let scope = cloud.get_credential_scope(&resource_id::RESOURCE_MANAGER).await?;
        let endpoint = cloud.get_endpoint(&endpoint::RESOURCE_MANAGER).await?;

        // let client = MgmtClient::build(ctx, credential, &options).await?;
        // client.send(request).await?;

        Ok(Some(json!({})))
    }
}

impl AzCommand for ShowDemo {
    const URI: &'static str = "crs://azure/plugin/demo/show";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "show".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "Show a demo resource".to_string(),
                long: Some("Show a demo resource".to_string()),
            }),
            args: Self::args(),
            positional_args: vec![],
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![
            CrsArg {
                var_name: "$Path.resourceGroupName".to_string(),
                name: "resourceGroupName".to_string(),
                options: vec!["name".to_string(), "n".to_string(), "g".to_string(), "resource-group".to_string()],
                help: Some(CrsArgHelp {
                    short: "Name of resource group.".to_string(),
                    ..Default::default()
                }),
                required: true,
                arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
                    format: Some(crs_string_arg::Format::String(CrsStringFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
            CrsArg {
                var_name: "$Path.subscriptionId".to_string(),
                name: "subscription".to_string(),
                options: vec!["subscription".to_string()],
                help: Some(CrsArgHelp {
                    short: "Name or ID of subscription.".to_string(),
                    ..Default::default()
                }),
                required: false,
                arg_type: Some(crs_arg::ArgType::String(CrsStringArg {
                    format: Some(crs_string_arg::Format::SubscriptionId(CrsSubscriptionIdFormat::default())),
                    ..Default::default()
                })),
                ..Default::default()
            },
        ]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError> {
        let mut show = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    "resourceGroupName" => {
                        show.resource_group_name = serde_json::from_slice(v).expect("failed to parse json");
                    }
                    "subscription" => {
                        show.subscription = serde_json::from_slice(v).expect("failed to parse json");
                    }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
        }
        Ok(show)
    }
}
