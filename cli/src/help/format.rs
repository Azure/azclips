// use az_core::error::AzResult;

use az_core::pb::command::CmdInterface;

pub struct CmdHelpFormatter {

}

impl CmdHelpFormatter {
    // pub fn format(&self) -> AzResult<String> {
    pub fn format<T>(&self, value: T) -> Option<String> where T: ?Sized + Into<CmdInterface> {
        let cmd: CmdInterface = value.into();
        todo!()
    }
}
