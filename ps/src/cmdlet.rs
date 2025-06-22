use std::str::FromStr;

use az_core::{
    pb::{crs_interface, CrsCommand, CrsCommandGroup, CrsInterface},
    uri::CrsUri,
};

pub fn retrieve_cmdlets(interface: &CrsInterface) -> Box<dyn Iterator<Item = CmdletInfo> + '_> {
    match interface.level.as_ref() {
        Some(crs_interface::Level::Command(command)) => Box::new(std::iter::once(command.into())),
        Some(crs_interface::Level::CommandGroup(command_group)) => {
            retrieve_cmdlets_from_command_group(command_group)
        }
        None => Box::new(std::iter::empty()),
    }
}

fn retrieve_cmdlets_from_command_group(
    group: &CrsCommandGroup,
) -> Box<dyn Iterator<Item = CmdletInfo> + '_> {
    let commands_iter = group.commands.iter().map(|c| c.into());
    let groups_iter = group
        .groups
        .iter()
        .flat_map(|nested_group| retrieve_cmdlets_from_command_group(nested_group));

    Box::new(commands_iter.chain(groups_iter))
}

pub struct CmdletInfo {
    pub uri: CrsUri,
}

impl From<&CrsCommand> for CmdletInfo {
    fn from(command: &CrsCommand) -> Self {
        let uri = CrsUri::from_str(&command.uri).unwrap();
        CmdletInfo { uri }
    }
}

impl CmdletInfo {
    // temporary solution, we should provide the ps name directly in the crs command interface metadata
    pub fn ps_name(&self) -> String {
        let uri = self.uri.segments().last().unwrap_or("invoke");
        let verb = match uri {
            "login" => "Connect",
            "logout" => "Disconnect",
            "add" => "Add",
            "create" => "New",
            "remove" => "Remove",
            "delete" => "Remove",
            "update" => "Update",
            "upgrade" => "Update",
            "list" => "Get",
            "get" => "Get",
            "show" => "Show", // TODO: need to combine the show and list for ps
            _ => "Invoke",
        };

        let segments = self.uri.segments().collect::<Vec<_>>();
        let name = if verb == "Invoke" {
            // Use all segments for Invoke
            segments
                .iter()
                .map(|s| to_camel_case(s))
                .collect::<Vec<_>>()
                .join("")
        } else {
            // Use all segments except the last one for other verbs
            segments
                .iter()
                .take(segments.len().saturating_sub(1))
                .map(|s| to_camel_case(s))
                .collect::<Vec<_>>()
                .join("")
        };

        format!("{}-{}", verb, name)
    }
}

fn to_camel_case(s: &str) -> String {
    let mut chars = s.chars();
    match chars.next() {
        None => String::new(),
        Some(first) => first.to_uppercase().collect::<String>() + &chars.as_str().to_lowercase(),
    }
}
