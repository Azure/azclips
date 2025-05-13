use crate::argparse::SHPartialKeys;

#[derive(Debug, PartialEq)]
pub struct ArgHelpOp {
    pub(crate) keys: SHPartialKeys,
}

impl ArgHelpOp {
    pub fn arg_keys(&self) -> &SHPartialKeys {
        &self.keys
    }
}
