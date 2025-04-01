use serde_json::Value;

use crate::argparse::partial::SHPartialKeys;

#[derive(Debug, PartialEq)]
pub struct ArgValueAssignOp {
    pub(crate) keys: SHPartialKeys,
    /// whether the value is filled from the blank value or not
    pub(crate) from_blank: bool,
    pub(crate) value: Value,
}
