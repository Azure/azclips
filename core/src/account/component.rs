use std::str::FromStr;

use async_trait::async_trait;

use crate::{
    cmd_uri::CmdUri,
    component::{AzComponent, AzComponentCtx},
    error::AzError,
    pb::{
        command::CmdInterface,
        component::{
            CheckRequest, CheckResponse, ExecuteRequest, ExecuteResponse, InterfaceRequest,
            InterfaceResponse, MetadataRequest, MetadataResponse,
        },
    },
};

/// The built-in component for managing the account
pub struct AccountComponent {}

#[async_trait]
impl<T: AzComponentCtx> AzComponent<T> for AccountComponent {
    async fn interface(
        &self,
        ctx: &T,
        request: InterfaceRequest,
    ) -> Result<InterfaceResponse, AzError> {
        todo!()
    }

    async fn execute(
        &self,
        ctx: &T,
        request: ExecuteRequest,
    ) -> Result<ExecuteResponse, AzError> {
        todo!()
    }

    async fn metadata(
        &self,
        ctx: &T,
        request: MetadataRequest,
    ) -> Result<MetadataResponse, AzError> {
        todo!()
    }

    async fn check(
        &self,
        ctx: &T,
        request: CheckRequest,
    ) -> Result<CheckResponse, AzError> {
        todo!()
    }
}

impl AccountComponent {
    const URI: &'static str = "cmd://azure/account/";
    pub fn uri() -> CmdUri {
        CmdUri::from_str(Self::URI).unwrap()
    }

    pub fn get_cmd_interface(depth: Option<u32>) -> CmdInterface {
        todo!()
    }
}
