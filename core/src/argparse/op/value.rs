use serde_json::Value;

use crate::argparse::SHPartialKeys;

#[derive(Debug, PartialEq)]
pub struct ArgValueAssignOp {
    pub(crate) keys: SHPartialKeys,
    pub(crate) is_append: bool,
    /// whether the value is filled from the blank value or not
    pub(crate) from_blank: bool,
    pub(crate) value: Value,
}
