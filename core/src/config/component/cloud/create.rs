use async_trait::async_trait;
use tracing::error;

use crate::{
    component::{AzCommand, AzCommandExecute, AzComponentCtx},
    config::cloud::{
        endpoint::{self, EndpointMap},
        resource_id::{self, ResourceIdMap},
        AzCloud, AzCloudMetadata,
    },
    error::AzError,
    pb::{
        cmd_arg, cmd_interface, CmdArg, CmdArgHelp, CmdCommand, CmdHelp, CmdInterface,
        CmdObjectArg, CmdObjectArgAdditionalProperties, CmdParams, CmdStringArg,
    },
    AzCore, VERSION,
};

#[derive(Debug, Default)]
pub struct CreateCloud {
    name: AzCloud,
    endpoints: EndpointMap,
    resource_ids: ResourceIdMap,
}

#[async_trait]
impl AzCommandExecute for CreateCloud {
    type Output = AzCloudMetadata;

    async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError> {
        let mut cloud_metadata = AzCloudMetadata {
            name: self.name.to_owned(),
            active: false,
            endpoints: self.endpoints.to_owned(),
            resource_ids: self.resource_ids.to_owned(),
        };
        {
            let core = AzCore::instance().await;
            let mut cfg = core.cfg.write().await;
            cloud_metadata.active = cfg.clouds.get_active().name == self.name;
            cfg.clouds.0.insert(self.name.to_owned(), cloud_metadata);
            cfg.save()?;
        }
        let cfg = ctx.get_cfg().await;
        let cloud = cfg.clouds.get_cloud(&self.name)?;
        Ok(Some(cloud))
    }
}

impl AzCommand for CreateCloud {
    const URI: &'static str = "cmd://azure/config/cloud/create";

    fn build_interface(_: Option<u32>) -> CmdInterface {
        CmdInterface {
            level: Some(cmd_interface::Level::Command(CmdCommand {
                name: "create".to_string(),
                uri: Self::uri().to_string(),
                version: VERSION.to_string(),
                help: Some(CmdHelp {
                    short: "create a cloud config".to_string(),
                    long: None,
                }),
                args: Self::args(),
                positional_args: vec![],
            })),
        }
    }

    fn args() -> Vec<CmdArg> {
        let mut endpoints = endpoint::StandardEndpoint::list_args(false);
        endpoints.extend(endpoint::StandardSuffix::list_args(false));
        let resource_ids = resource_id::StandardResourceId::list_args(false);
        vec![
            CmdArg {
                var_name: "name".to_string(),
                name: "name".to_string(),
                help: Some(CmdArgHelp {
                    short: "The name of the cloud".to_string(),
                    long: None,
                }),
                options: vec!["name".to_string()],
                required: true,
                arg_type: Some(cmd_arg::ArgType::String(CmdStringArg {
                    ..Default::default()
                })),
                ..Default::default()
            },
            CmdArg {
                var_name: "endpoints".to_string(),
                name: "endpoints".to_string(),
                help: Some(CmdArgHelp {
                    short: "The endpoints used by the cloud".to_string(),
                    long: None,
                }),
                options: vec!["endpoints".to_string()],
                required: true,
                arg_type: Some(cmd_arg::ArgType::Object(Box::new(CmdObjectArg {
                    props: endpoints,
                    additional_props: Some(Box::new(CmdObjectArgAdditionalProperties {
                        item: Some(Box::new(CmdArg {
                            var_name: "endpoints{}".to_string(),
                            name: "endpoint".to_string(),
                            help: Some(CmdArgHelp {
                                short: "The custom endpoint used in the cloud".to_string(),
                                long: None,
                            }),
                            arg_type: Some(cmd_arg::ArgType::String(CmdStringArg {
                                ..Default::default()
                            })),
                            ..Default::default()
                        })),
                    })),
                    ..Default::default()
                }))),
                ..Default::default()
            },
            CmdArg {
                var_name: "resourceIds".to_string(),
                name: "resourceIds".to_string(),
                help: Some(CmdArgHelp {
                    short: "The resource ids used by the cloud".to_string(),
                    long: None,
                }),
                options: vec!["resource-ids".to_string()],
                required: true,
                arg_type: Some(cmd_arg::ArgType::Object(Box::new(CmdObjectArg {
                    props: resource_ids,
                    additional_props: Some(Box::new(CmdObjectArgAdditionalProperties {
                        item: Some(Box::new(CmdArg {
                            var_name: "resourceIds{}".to_string(),
                            name: "resourceId".to_string(),
                            help: Some(CmdArgHelp {
                                short: "The custom resource id used in the cloud".to_string(),
                                long: None,
                            }),
                            arg_type: Some(cmd_arg::ArgType::String(CmdStringArg {
                                ..Default::default()
                            })),
                            ..Default::default()
                        })),
                    })),
                    ..Default::default()
                }))),
                ..Default::default()
            },
        ]
    }

    fn try_from(params: Option<CmdParams>) -> Result<Self, AzError> {
        let mut create_cloud = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    "name" => {
                        create_cloud.name = serde_json::from_slice::<String>(v)
                            .expect("failed to parse json")
                            .parse()
                            .expect("failed to convert to AzCloud");
                    }
                    "endpoints" => {
                        create_cloud.endpoints = serde_json::from_slice::<EndpointMap>(v)
                            .expect("failed to parse json");
                    }
                    "resourceIds" => {
                        create_cloud.resource_ids = serde_json::from_slice::<ResourceIdMap>(v)
                            .expect("failed to parse json");
                    }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
        }
        Ok(create_cloud)
    }
}
