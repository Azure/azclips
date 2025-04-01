use az_core::{
    argparse::{self, arg::CmdArgWrapper, ArgOp, ArgValueAssignOp},
    cmd_uri::CmdUri,
    error::{AzError, AzInternalError, AzUserUsageError},
    pb::command::{cmd_interface, CmdArg, CmdCommand},
    AzCore,
};

use crate::{help, CmdRawArg};

pub fn execute_command(cmd_uri: CmdUri, raw_args: Vec<CmdRawArg>) {
    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        if let Err(e) = execute_command_async(cmd_uri, raw_args).await {
            // TODO: handle the error, need to add error handler in the core
            eprintln!("{}", e);
            std::process::exit(1);
        }
    });
}

async fn execute_command_async(cmd_uri: CmdUri, raw_args: Vec<CmdRawArg>) -> Result<(), AzError> {
    let core = AzCore::instance().await;
    let cmd_uri = core.check_cmd_uri(cmd_uri).await?;

    if !cmd_uri.executable() {
        // if it's a command group, then show the help of it instead of executing it
        help::show_command_help_async(cmd_uri).await?;
        return Err(AzUserUsageError::CommandGroupNotExecutableError.into());
    }

    let cmd_interface = core.get_cmd_interface(&cmd_uri).await?;
    let cmd = match cmd_interface.level.unwrap() {
        cmd_interface::Level::Command(cmd) => cmd,
        _ => {
            unreachable!()
        }
    };

    let mut cmd_value_ops = Vec::new();

    // the positional arguments will be returned as params in uri query part by service
    for (arg_name, values) in cmd_uri.params() {
        let (arg, ops) = parse_arg_values(&cmd, arg_name, values)?;

        // show the help of the argument
        if let Some(ArgOp::Help(op)) = ops.iter().find(|op| matches!(op, ArgOp::Help(_))) {
            return help::show_command_arg_help_async(cmd_uri.clone(), op.arg_keys().to_owned())
                .await;
        }

        let value_ops = retrieve_value_ops(&arg, ops)?;
        cmd_value_ops.extend(value_ops);
    }

    for (arg_name, values) in raw_args
        .iter()
        .map(|raw_arg| (raw_arg.name_or_alias(), raw_arg.values()))
    {
        let (arg, ops) = parse_arg_values(&cmd, arg_name, values)?;

        // show the help of the argument
        if let Some(ArgOp::Help(op)) = ops.iter().find(|op| matches!(op, ArgOp::Help(_))) {
            return help::show_command_arg_help_async(cmd_uri, op.arg_keys().to_owned()).await;
        }

        let value_ops = retrieve_value_ops(&arg, ops)?;
        cmd_value_ops.extend(value_ops);
    }

    // TODO: filter some global args such as --output etc
    let output = core.execute_cmd(&cmd_uri, cmd_value_ops).await?;

    // TODO: format the output
    if let Some(data) = output {
        println!(
            "{}",
            serde_json::to_string_pretty(&data).map_err(AzInternalError::Json)?
        );
    }

    Ok(())
}

fn parse_arg_values<'a, 'b>(
    cmd: &'a CmdCommand,
    arg_name: &str,
    values: impl IntoIterator<Item = &'b str>,
) -> Result<(CmdArgWrapper<'a>, Vec<ArgOp>), AzError> {
    if let Some(arg) = find_arg_in_global(arg_name) {
        let wrapper = CmdArgWrapper::new(None, arg)?;
        let ops = argparse::parse_arg_values(&wrapper, values)?;
        Ok((wrapper, ops))
    } else if let Some(arg) = find_arg_in_command(arg_name, &cmd) {
        let wrapper = CmdArgWrapper::new(Some(&cmd), arg)?;
        let ops = argparse::parse_arg_values(&wrapper, values)?;
        Ok((wrapper, ops))
    } else {
        Err(AzUserUsageError::UnrecognizedArgumentError(arg_name.to_string()).into())
    }
}

/// This function will retrieve the value ops from the ArgOp
/// When an operation is the prompt input operation, it will get value from stdin
fn retrieve_value_ops(
    arg: &CmdArgWrapper<'_>,
    ops: Vec<ArgOp>,
) -> Result<Vec<ArgValueAssignOp>, AzError> {
    let mut value_ops = Vec::with_capacity(ops.len());
    for op in ops.into_iter() {
        let value_op = match op {
            ArgOp::Prompt(op) => {
                // TODO: customize the argument input and output
                let mut input = std::io::stdin();
                let mut output = std::io::stderr();
                let parts = op.arg_keys().parts().collect::<Vec<_>>();
                if parts.len() == 1 {
                    op.prompt_input(&arg, &mut input, &mut output)?
                } else {
                    let wrapper = arg.retrieve_sub_arg(parts[1..].iter().map(|p| *p))?;
                    op.prompt_input(&wrapper, &mut input, &mut output)?
                }
            }
            ArgOp::Value(value_op) => value_op,
            ArgOp::Help(_) => {
                unreachable!()
            }
        };
        value_ops.push(value_op);
    }
    Ok(value_ops)
}

fn find_arg_in_global(_: &str) -> Option<&'static CmdArg> {
    // TODO: support global args definition
    return None;
}

fn find_arg_in_command<'a>(name_or_alias: &str, cmd: &'a CmdCommand) -> Option<&'a CmdArg> {
    for arg in cmd.args.iter() {
        if arg.name.eq_ignore_ascii_case(name_or_alias) {
            return Some(arg);
        }
        if arg
            .options
            .iter()
            .any(|o| o.eq_ignore_ascii_case(name_or_alias))
        {
            return Some(arg);
        }
    }
    None
}
