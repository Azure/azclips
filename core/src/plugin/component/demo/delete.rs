use async_trait::async_trait;
use serde_json::{json, Value};
use tracing::error;

use crate::{
    client::{ClientCredential, MgmtClient, Method},
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    error::AzError,
    pb::{
        crs_arg, crs_string_arg, CrsArg, CrsArgHelp, CrsCommand, CrsHelp, CrsParams, CrsStringArg,
        CrsStringFormat, CrsSubscriptionIdFormat,
    },
        VERSION,
};

#[derive(Debug, Default)]
pub struct DeleteDemo {
    resource_group_name: String,
    subscription: Option<String>,
}

#[async_trait]
impl AzCommandExecute for DeleteDemo {
    type Output = ();

    async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError> {
        let subscription = match self.subscription.as_ref() {
            Some(s) => s.to_owned(),
            None => ctx.get_default_subscription().await?,
        };

        let credential = ctx.get_default_credential().await?;
        let client_credential = ClientCredential::new(credential.clone());

        // How to define the telemetry options?
        let options = crate::client::options::ClientOptions {
            telemetry: crate::client::options::TelemetryOptions {
                component_name: "crs://azure/plugin/demo/show".to_string(),
                component_version: "0.0.1".to_string(),
            },
        };

        let client = MgmtClient::build(ctx, client_credential, &options).await?;

        let path = format!(
            "/subscriptions/{}/resourceGroups/{}/",
            subscription, self.resource_group_name
        );
        let query_pairs = vec![("api-version".to_string(), "2020-06-01".to_string())];
        let response = client
            .run(Method::Delete, &path, Some(query_pairs), None, None, None)
            .await?;

            let status = response.status_code;
            if !status.is_success() {
                return Err(AzError::GeneralError(format!(
                    "Failed to get resource group info: status code {}",
                    status
                )));
            }

        Ok(None)
    }
}

impl AzCommand for DeleteDemo {
    const URI: &'static str = "crs://azure/plugin/demo/delete";

    fn build_interface(_: Option<u32>) -> CrsCommand {
        CrsCommand {
            name: "delete".to_string(),
            uri: Self::uri().to_string(),
            version: VERSION.to_string(),
            help: Some(CrsHelp {
                short: "Delete a demo resource".to_string(),
                long: Some("Delete a demo resource".to_string()),
            }),
            args: Self::args(),
            ..Default::default()
        }
    }

    fn args() -> Vec<CrsArg> {
        vec![
            CrsArg {
                var_name: "$Path.resourceGroupName".to_string(),
                name: "resourceGroupName".to_string(),
                options: vec![
                    "name".to_string(),
                    "n".to_string(),
                    "g".to_string(),
                    "resource-group".to_string(),
                ],
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
                    format: Some(crs_string_arg::Format::SubscriptionId(
                        CrsSubscriptionIdFormat::default(),
                    )),
                    ..Default::default()
                })),
                ..Default::default()
            },
        ]
    }

    fn try_from(params: Option<CrsParams>) -> Result<Self, AzError> {
        let mut delete = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    "resourceGroupName" => {
                        delete.resource_group_name =
                            serde_json::from_slice(v).expect("failed to parse json");
                    }
                    "subscription" => {
                        delete.subscription =
                            serde_json::from_slice(v).expect("failed to parse json");
                    }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
        }
        Ok(delete)
    }
}
