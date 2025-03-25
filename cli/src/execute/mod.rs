use az_core::{cmd_uri::CmdUri, AzCore};

use crate::CmdRawArg;

pub fn execute_command(cmd_uri: &CmdUri, raw_args: &[CmdRawArg]) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(execute_command_async(cmd_uri, raw_args));
}

async fn execute_command_async(cmd_uri: &CmdUri, raw_args: &[CmdRawArg]) {
    let core = AzCore::instance().await;

    

    // let plugin = core.get_plugin(cmd_uri).await?;

    unimplemented!()
}
