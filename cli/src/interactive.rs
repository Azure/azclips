use az_core::{error::AzError, uri::CrsUri, AzCore};

use crate::ArgRaw;

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_interactive_flag(raw_args: &[ArgRaw]) -> bool {
    raw_args.iter().any(|arg| arg.flag == "--cli-interactive")
}

pub fn run_interactive(uri: CrsUri, raw_args: Vec<ArgRaw>) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        if let Err(e) = run_interactive_async(uri, raw_args).await {
            // TODO: handle the error, need to add error handler in the core
            eprintln!("{}", e);
            std::process::exit(1);
        }
    });
}

async fn run_interactive_async(uri: CrsUri, raw_args: Vec<ArgRaw>) -> Result<(), AzError> {
    let core = AzCore::instance().await;

    todo!()
}
