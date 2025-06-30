use std::ffi::CString;
use std::os::raw::c_char;
use std::str::FromStr;

use az_core::{
    pb::{crs_interface, CrsCommand, CrsCommandGroup, CrsInterface},
    uri::CrsUri,
};

pub struct CmdletInfo {
    pub uri: CrsUri,
    pub synopsis: String,
    pub description: Option<String>,
}

// C-compatible structure for direct array passing
#[repr(C)]
pub struct CCmdletInfo {
    pub name: *mut c_char,
    pub uri: *mut c_char,
    pub synopsis: *mut c_char,
    pub description: *mut c_char,
}

// C-compatible array structure
#[repr(C)]
pub struct CCmdletArray {
    pub items: *mut CCmdletInfo,
    pub count: usize,
}

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

impl From<&CrsCommand> for CmdletInfo {
    fn from(command: &CrsCommand) -> Self {
        let uri = CrsUri::from_str(&command.uri).unwrap();

        CmdletInfo {
            uri,
            synopsis: command
                .help
                .as_ref()
                .map(|h| h.short.clone())
                .unwrap_or_default(),
            description: command
                .help
                .as_ref()
                .map(|h| h.long.clone())
                .unwrap_or_default(),
        }
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
            "patch" => "Update",
            "put" => "Set",
            "set" => "Set",
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
    s.split('-')
        .map(|word| {
            let mut chars = word.chars();
            match chars.next() {
                None => String::new(),
                Some(first) => {
                    first.to_uppercase().collect::<String>() + &chars.as_str().to_lowercase()
                }
            }
        })
        .collect::<Vec<_>>()
        .join("")
}

/// Convert cmdlets to C array structure
pub fn convert_cmdlets_to_c_array(cmdlets: Vec<CmdletInfo>) -> *mut CCmdletArray {
    if cmdlets.is_empty() {
        return Box::into_raw(Box::new(CCmdletArray {
            items: std::ptr::null_mut(),
            count: 0,
        }));
    }

    let mut c_cmdlets = Vec::with_capacity(cmdlets.len());

    for cmdlet in cmdlets {
        let name_cstring = match CString::new(cmdlet.ps_name()) {
            Ok(s) => s,
            Err(_) => continue,
        };
        let uri_cstring = match CString::new(cmdlet.uri.to_string()) {
            Ok(s) => s,
            Err(_) => continue,
        };
        let synopsis_cstring = match CString::new(cmdlet.synopsis) {
            Ok(s) => s,
            Err(_) => match CString::new("") {
                Ok(s) => s,
                Err(_) => continue,
            },
        };
        let description_cstring = match CString::new(cmdlet.description.unwrap_or_default()) {
            Ok(s) => s,
            Err(_) => match CString::new("") {
                Ok(s) => s,
                Err(_) => continue,
            },
        };

        c_cmdlets.push(CCmdletInfo {
            name: name_cstring.into_raw(),
            uri: uri_cstring.into_raw(),
            synopsis: synopsis_cstring.into_raw(),
            description: description_cstring.into_raw(),
        });
    }

    let count = c_cmdlets.len();
    let items = c_cmdlets.into_boxed_slice();
    let items_ptr = Box::into_raw(items) as *mut CCmdletInfo;

    let array = Box::new(CCmdletArray {
        items: items_ptr,
        count,
    });

    Box::into_raw(array)
}
