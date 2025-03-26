use az_core::{cmd_uri::CmdUri, error::AzError, AzCore};

use crate::CmdRawArg;

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_interactive_flag(raw_args: &[CmdRawArg]) -> bool {
    raw_args.iter().any(|arg| arg.flag == "--cli-interactive")
}

pub fn run_interactive(cmd_uri: CmdUri, raw_args: Vec<CmdRawArg>) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        if let Err(e) = run_interactive_async(cmd_uri, raw_args).await {
            // TODO: handle the error, need to add error handler in the core
            eprintln!("{}", e);
            std::process::exit(1);
        }
    });
}

async fn run_interactive_async(cmd_uri: CmdUri, raw_args: Vec<CmdRawArg>) -> Result<(), AzError> {
    let core = AzCore::instance().await;

    todo!()
}
