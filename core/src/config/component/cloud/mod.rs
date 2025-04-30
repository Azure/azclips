mod list;
mod register;
mod set;
mod show;
mod unregister;
mod update;

use std::str::FromStr;

use async_trait::async_trait;
use list::ListClouds;

use crate::{
    cmd_uri::CmdUri,
    component::{
        command::{AzCmdGroup, AzCmdRoute},
        AzComponent, AzComponentCtx,
    },
    error::{AzError, AzInternalError},
    pb::{
        self,
        command::{cmd_interface, CmdCommandGroup, CmdComponent, CmdHelp, CmdInterface},
        component::{
            execute_response, CheckRequest, CheckResponse, ComponentMetadata, ExecuteRequest,
            ExecuteResponse, InterfaceRequest, InterfaceResponse, MetadataRequest,
            MetadataResponse,
        },
    },
};

#[derive(Debug, Default)]
pub(super) struct CloudCommandGroup {}

impl AzCmdRoute for CloudCommandGroup {
    const URI: &'static str = "cmd://azure/config/cloud/";

    fn cmd_interface(depth: Option<u32>) -> CmdInterface {
        let groups = vec![];
        let mut commands = vec![];
        if depth.is_none() || depth.unwrap() > 0 {
            let depth = depth.map(|d| d - 1);
            commands.push(ListClouds::cmd_interface(depth));
        }
        CmdInterface {
            name: "cloud".to_string(),
            help: Some(CmdHelp {
                short: "Manage registered Azure clouds configuration".to_string(),
                long: None,
            }),
            uri: Self::uri().to_string(),
            level: Some(cmd_interface::Level::Group(CmdCommandGroup {
                groups,
                commands,
            })),
        }
    }
}

impl AzCmdGroup for CloudCommandGroup {
    fn get_interface(
        depth: Option<u32>,
        segments: &mut dyn Iterator<Item = &str>,
    ) -> Option<CmdInterface> {
        match segments.next() {
            None => Some(CloudCommandGroup::cmd_interface(depth)),
            Some("list") => Some(ListClouds::cmd_interface(depth)),
            _ => None,
        }
    }

    fn check_uri(segments: &mut dyn Iterator<Item = &str>) -> Option<CmdUri> {
        match segments.next() {
            None => Some(CloudCommandGroup::uri()),
            Some("list") => Some(ListClouds::uri()),
            _ => None,
        }
    }
}
