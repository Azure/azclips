use std::env::Args;

use az_core::{uri::CrsUri, APPLICATION_NAME};

mod execute;
mod help;
mod interactive;
mod output;

pub(crate) const ARG_FLAG_PREFIX: char = '-';

#[derive(Debug)]
pub(crate) struct ArgRaw {
    // the flag used in the input
    flag: String,
    // the raw values of the parameter
    values: Vec<String>,
}

impl ArgRaw {
    pub fn name_or_alias(&self) -> &str {
        self.flag.trim_start_matches('-')
    }

    pub fn values(&self) -> impl Iterator<Item = &str> {
        self.values.iter().map(|s| s.as_str())
    }
}

pub fn run(mut args: Args) {
    let mut next = args.next();

    // retrieve the cmd uri
    let mut uri = vec![APPLICATION_NAME.to_string()];
    while let Some(arg) = next.as_ref() {
        if arg.starts_with(ARG_FLAG_PREFIX) {
            break;
        }
        uri.push(arg.to_lowercase());
        next = args.next();
    }
    let uri = CrsUri::new(uri, false); // here we assume the command is not executable, and the service will correct it if it's a executable command

    // parse cmd args
    let mut raw_args = Vec::new();
    while let Some(flag) = next.take() {
        // parse as cmd arg
        if !flag.starts_with(ARG_FLAG_PREFIX) {
            unreachable!("argument flag should always start with '-' when parsing cmd args");
        }
        let mut raw_arg = ArgRaw {
            flag,
            values: Vec::new(),
        };
        while let Some(value) = args.next() {
            if value.starts_with(ARG_FLAG_PREFIX) {
                next.replace(value);
                break;
            } else {
                raw_arg.values.push(value);
            }
        }
        raw_args.push(raw_arg);
    }

    // run interactive if the interactive flag is present
    // the interactive check should be before the help check, because the interactive mode will show the help info
    if interactive::has_interactive_flag(&raw_args) {
        interactive::run_interactive(uri, raw_args);
        return;
    }

    // show help if the help flag is present
    if help::has_help_flag(&raw_args) {
        help::show_command_help(uri);
        return;
    }

    // execute the command
    execute::execute_command(uri, raw_args);
}
