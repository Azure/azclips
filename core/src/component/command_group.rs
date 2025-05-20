use std::str::FromStr;

use async_trait::async_trait;

use crate::{
    error::AzError,
    pb::{execute_response, CrsCommandGroup, CrsInterface, CrsOutputFormat, CrsParams},
    uri::CrsUri,
};

use super::AzComponentCtx;

#[async_trait]
pub trait AzCommandGroup {
    const URI: &'static str;

    fn uri() -> CrsUri {
        CrsUri::from_str(Self::URI).unwrap()
    }

    fn build_interface(depth: Option<u32>) -> CrsCommandGroup;

    fn find_interface(
        depth: Option<u32>,
        segments: impl Iterator<Item = String>,
    ) -> Option<CrsInterface>;

    fn resolve_uri(segments: impl Iterator<Item = String>) -> Option<CrsUri>;

    async fn execute_group<T: AzComponentCtx>(
        ctx: &T,
        segments: impl Iterator<Item = String> + Send,
        params: Option<CrsParams>,
        output_format: Option<CrsOutputFormat>,
    ) -> Result<execute_response::Data, AzError>;
}
