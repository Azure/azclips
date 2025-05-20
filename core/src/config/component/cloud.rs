mod activate;
mod create;
mod delete;
mod list;
mod show;
mod update;

use async_trait::async_trait;

use activate::ActivateCloud;
use create::CreateCloud;
use delete::DeleteCloud;
use list::ListClouds;
use show::ShowCloud;
use update::UpdateCloud;

use crate::{
    component::{AzCommand, AzCommandGroup, AzComponentCtx},
    error::{AzError, AzInternalError},
    pb::{execute_response, CrsCommandGroup, CrsHelp, CrsInterface, CrsOutputFormat, CrsParams},
    uri::CrsUri,
};

#[derive(Debug, Default)]
pub struct CloudCommandGroup {}

#[async_trait]
impl AzCommandGroup for CloudCommandGroup {
    const URI: &'static str = "crs://azure/config/cloud/";

    fn build_interface(depth: Option<u32>) -> CrsCommandGroup {
        let groups = vec![];
        let mut commands = vec![];
        if depth.is_none() || depth.unwrap() > 0 {
            let depth = depth.map(|d| d - 1);
            commands.push(ListClouds::build_interface(depth));
            commands.push(ShowCloud::build_interface(depth));
            commands.push(DeleteCloud::build_interface(depth));
            commands.push(ActivateCloud::build_interface(depth));
            commands.push(CreateCloud::build_interface(depth));
            commands.push(UpdateCloud::build_interface(depth));
        }
        CrsCommandGroup {
            name: "cloud".to_string(),
            uri: Self::uri().to_string(),
            help: Some(CrsHelp {
                short: "Manage registered Azure clouds configuration".to_string(),
                long: None,
            }),
            groups,
            commands,
        }
    }

    fn find_interface(
        depth: Option<u32>,
        mut segments: impl Iterator<Item = String>,
    ) -> Option<CrsInterface> {
        match segments.next().as_deref() {
            None => Some(CloudCommandGroup::build_interface(depth).into()),
            Some("activate") => Some(ActivateCloud::build_interface(depth).into()),
            Some("create") => Some(CreateCloud::build_interface(depth).into()),
            Some("update") => Some(UpdateCloud::build_interface(depth).into()),
            Some("show") => Some(ShowCloud::build_interface(depth).into()),
            Some("delete") => Some(DeleteCloud::build_interface(depth).into()),
            Some("list") => Some(ListClouds::build_interface(depth).into()),
            _ => None,
        }
    }

    fn resolve_uri(mut segments: impl Iterator<Item = String>) -> Option<CrsUri> {
        match segments.next().as_deref() {
            None => Some(CloudCommandGroup::uri()),
            Some("activate") => Some(ActivateCloud::uri()),
            Some("create") => Some(CreateCloud::uri()),
            Some("update") => Some(UpdateCloud::uri()),
            Some("show") => Some(ShowCloud::uri()),
            Some("delete") => Some(DeleteCloud::uri()),
            Some("list") => Some(ListClouds::uri()),
            _ => None,
        }
    }

    async fn execute_group<T: AzComponentCtx>(
        ctx: &T,
        mut segments: impl Iterator<Item = String> + Send,
        params: Option<CrsParams>,
        output_format: Option<CrsOutputFormat>,
    ) -> Result<execute_response::Data, AzError> {
        match segments.next().as_deref() {
            Some("activate") => ActivateCloud::execute(ctx, params, output_format).await,
            Some("create") => CreateCloud::execute(ctx, params, output_format).await,
            Some("update") => UpdateCloud::execute(ctx, params, output_format).await,
            Some("show") => ShowCloud::execute(ctx, params, output_format).await,
            Some("delete") => DeleteCloud::execute(ctx, params, output_format).await,
            Some("list") => ListClouds::execute(ctx, params, output_format).await,
            _ => Err(AzInternalError::InvalidCrsUriError(Self::uri().to_string()).into()),
        }
    }
}
