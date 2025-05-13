use std::collections::HashMap;

use async_trait::async_trait;
use tracing::error;

use crate::{
    component::{AzCmd, AzCmdExecute, AzComponentCtx},
    config::cloud::{
        endpoint::{self, EndpointMap, EndpointName},
        resource_id::{self, ResourceIdMap, ResourceIdName},
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
pub struct UpdateCloud {
    name: AzCloud,
    endpoints: HashMap<EndpointName, Option<String>>,
    resource_ids: HashMap<ResourceIdName, Option<String>>,
    // internal property which will not exposed
    _patch_keys: Vec<String>,
}

#[async_trait]
impl AzCmdExecute for UpdateCloud {
    type Output = AzCloudMetadata;

    async fn run<T: AzComponentCtx>(&self, ctx: &T) -> Result<Option<Self::Output>, AzError> {
        let mut cloud_metadata = ctx.get_cfg().await.clouds.get_cloud(&self.name)?;

        if self._patch_keys.is_empty() || self._patch_keys.contains(&"endpoints".to_string()) {
            let endpoints = self
                .endpoints
                .iter()
                .filter(|(_, value)| value.is_some())
                .map(|(key, value)| (key, value.as_deref().unwrap()));
            let endpoints = EndpointMap::from(endpoints);
            cloud_metadata.endpoints = endpoints;
        } else {
            for (name, value) in self.endpoints.iter() {
                match value {
                    Some(value) => {
                        cloud_metadata.endpoints.insert(name.to_owned(), value);
                    }
                    None => {
                        cloud_metadata.endpoints.remove(name);
                    }
                }
            }
        }

        if self._patch_keys.is_empty() || self._patch_keys.contains(&"resourceIds".to_string()) {
            let resource_ids = self
                .resource_ids
                .iter()
                .filter(|(_, value)| value.is_some())
                .map(|(key, value)| (key, value.as_deref().unwrap()));
            let resource_ids = ResourceIdMap::from(resource_ids);
            cloud_metadata.resource_ids = resource_ids;
        } else {
            for (name, value) in self.resource_ids.iter() {
                match value {
                    Some(value) => {
                        cloud_metadata.resource_ids.insert(name.to_owned(), value);
                    }
                    None => {
                        cloud_metadata.resource_ids.remove(name);
                    }
                }
            }
        }

        {
            let core = AzCore::instance().await;
            let mut cfg = core.cfg.write().await;
            cfg.clouds.0.insert(self.name.to_owned(), cloud_metadata);
            cfg.save()?;
        }

        let cfg = ctx.get_cfg().await;
        let cloud_metadata = cfg.clouds.get_cloud(&self.name)?;
        Ok(Some(cloud_metadata))
    }
}

impl AzCmd for UpdateCloud {
    const URI: &'static str = "cmd://azure/config/cloud/update";

    fn cmd_interface(_: Option<u32>) -> CmdInterface {
        CmdInterface {
            name: "update".to_string(),
            help: Some(CmdHelp {
                short: "update a cloud config".to_string(),
                long: None,
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
        let mut endpoints = endpoint::StandardEndpoint::list_args(true);
        endpoints.extend(endpoint::StandardSuffix::list_args(true));
        let resource_ids = resource_id::StandardResourceId::list_args(true);
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
                    short: "The endpoints of the cloud".to_string(),
                    long: None,
                }),
                options: vec!["endpoints".to_string()],
                required: false,
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
                    short: "The resource ids of the cloud".to_string(),
                    long: None,
                }),
                options: vec!["resource-ids".to_string()],
                required: false,
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
        let mut update_cloud = Self::default();
        if let Some(params) = params {
            for (name, v) in params.params.iter() {
                match name.as_str() {
                    "name" => {
                        update_cloud.name = serde_json::from_slice::<String>(v)
                            .expect("failed to parse json")
                            .parse()
                            .expect("failed to convert to AzCloud");
                    }
                    "endpoints" => {
                        update_cloud.endpoints =
                            serde_json::from_slice::<HashMap<EndpointName, Option<String>>>(v)
                                .expect("failed to parse json");
                    }
                    "resourceIds" => {
                        update_cloud.resource_ids =
                            serde_json::from_slice::<HashMap<ResourceIdName, Option<String>>>(v)
                                .expect("failed to parse json");
                    }
                    _ => {
                        error!("unknown argument: {}", name);
                    }
                }
            }
            update_cloud._patch_keys = params.patch_keys;
        }
        Ok(update_cloud)
    }
}
