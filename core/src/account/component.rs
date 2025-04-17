use std::str::FromStr;

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
pub enum AccountComponent {}

impl<T: AzComponentCtx> AzComponent<T> for AccountComponent {
    fn interface<'a>(
        &'a self,
        ctx: &'a T,
        request: InterfaceRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = Result<InterfaceResponse, AzError>> + Send + 'a>,
    > {
        todo!()
    }

    fn execute<'a>(
        &'a self,
        ctx: &'a T,
        request: ExecuteRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = Result<ExecuteResponse, AzError>> + Send + 'a>,
    > {
        todo!()
    }

    fn metadata<'a>(
        &'a self,
        ctx: &'a T,
        request: MetadataRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = Result<MetadataResponse, AzError>> + Send + 'a>,
    > {
        todo!()
    }

    fn check<'a>(
        &'a self,
        ctx: &'a T,
        request: CheckRequest,
    ) -> std::pin::Pin<
        Box<dyn std::future::Future<Output = Result<CheckResponse, AzError>> + Send + 'a>,
    > {
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
