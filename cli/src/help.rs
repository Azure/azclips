pub mod format;
mod table;
mod templates;

use az_core::{
    argparse::{CrsArgWrapper, SHPartialKeys},
    error::AzError,
    pb::CrsCommand,
    uri::CrsUri,
    AzCore,
};
use format::default_help_formatter;

use crate::ArgRaw;

/// Minimum width for the detail content
/// This is used after considering the intent
/// Use infinite when the width is not available
pub const MIN_DETAIL_WIDTH: usize = 20;

/// check if the `-h` or `--help` flag is present in the raw args
pub fn has_help_flag(raw_args: &[ArgRaw]) -> bool {
    raw_args
        .iter()
        .any(|arg| arg.flag == "-h" || arg.flag == "--help")
}

pub fn show_command_help(uri: CrsUri) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        if let Err(e) = show_command_help_async(uri).await {
            // TODO: handle the error, need to add error handler in the core
            eprintln!("{}", e);
            std::process::exit(1);
        }
    });
}

pub(crate) async fn show_command_help_async(uri: CrsUri) -> Result<(), AzError> {
    let term_width = crossterm::terminal::size().ok().map(|(w, _)| w as usize);
    let core = AzCore::instance().await;
    let invocation = core.resolve_invocation(uri).await?;
    if invocation.uri.executable() {
        let crs_interface = core.get_interface(&invocation).await?;
        // TODO: global arguments
        for line in default_help_formatter().format_cmd(&crs_interface, None, term_width) {
            println!("{}", line);
        }
        Ok(())
    } else {
        let crs_interface = core.get_interface(&invocation).await?;
        let sub_components = core.get_metadata_of_sub_components(&invocation.uri).await?;
        for line in
            default_help_formatter().format_cmd(&crs_interface, sub_components.values(), term_width)
        {
            println!("{}", line);
        }
        Ok(())
    }
}

pub(crate) async fn show_command_arg_help_async(
    command: &CrsCommand,
    keys: SHPartialKeys,
) -> Result<(), AzError> {
    let wrapper = CrsArgWrapper::build_by_keys(command, &keys)?;
    let arg = wrapper.arg();
    let term_width = crossterm::terminal::size().ok().map(|(w, _)| w as usize);
    for line in default_help_formatter().format_arg(arg, term_width) {
        println!("{}", line);
    }
    Ok(())
}
