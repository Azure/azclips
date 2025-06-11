mod create;
mod delete;
mod list;
mod show;
mod update;

use async_trait::async_trait;
use create::CreateDemo;
use delete::DeleteDemo;
use list::ListDemo;
use show::ShowDemo;
use update::UpdateDemo;

use crate::{
    component::{AzCommand, AzCommandGroup, AzComponentCtx},
    error::{AzError, AzInternalError},
    pb::{execute_response, CrsCommandGroup, CrsHelp, CrsInterface, CrsOutputFormat, CrsParams},
    uri::CrsUri,
};

#[derive(Debug, Default)]
pub struct DemoCommandGroup {}

#[async_trait]
impl AzCommandGroup for DemoCommandGroup {
    const URI: &'static str = "crs://azure/plugin/demo/";

    fn build_interface(depth: Option<u32>) -> CrsCommandGroup {
        let groups = vec![];
        let mut commands = vec![];
        if depth.is_none() || depth.unwrap() > 0 {
            let depth = depth.map(|d| d - 1);
            commands.push(ListDemo::build_interface(depth));
            commands.push(ShowDemo::build_interface(depth));
            commands.push(DeleteDemo::build_interface(depth));
            commands.push(UpdateDemo::build_interface(depth));
            commands.push(CreateDemo::build_interface(depth));
        }
        CrsCommandGroup {
            name: "demo".to_string(),
            uri: Self::uri().to_string(),
            help: Some(CrsHelp {
                short: "demo resource for CRUD".to_string(),
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
            None => Some(DemoCommandGroup::build_interface(depth).into()),
            Some("list") => Some(ListDemo::build_interface(depth).into()),
            Some("show") => Some(ShowDemo::build_interface(depth).into()),
            Some("delete") => Some(DeleteDemo::build_interface(depth).into()),
            Some("update") => Some(UpdateDemo::build_interface(depth).into()),
            Some("create") => Some(CreateDemo::build_interface(depth).into()),
            _ => None,
        }
    }

    fn resolve_uri(mut segments: impl Iterator<Item = String>) -> Option<CrsUri> {
        match segments.next().as_deref() {
            None => Some(DemoCommandGroup::uri()),
            Some("list") => Some(ListDemo::uri()),
            Some("show") => Some(ShowDemo::uri()),
            Some("delete") => Some(DeleteDemo::uri()),
            Some("update") => Some(UpdateDemo::uri()),
            Some("create") => Some(CreateDemo::uri()),
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
            Some("list") => ListDemo::execute(ctx, params, output_format).await,
            Some("show") => ShowDemo::execute(ctx, params, output_format).await,
            Some("delete") => DeleteDemo::execute(ctx, params, output_format).await,
            Some("update") => UpdateDemo::execute(ctx, params, output_format).await,
            Some("create") => CreateDemo::execute(ctx, params, output_format).await,
            _ => Err(AzInternalError::InvalidCrsUriError(Self::uri().to_string()).into()),
        }
    }
}
