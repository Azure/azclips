use serde_json::Value;

use crate::argparse::SHPartialKeys;

#[derive(Debug, PartialEq)]
pub struct ArgValueAssignOp {
    pub keys: SHPartialKeys,
    pub is_append: bool,
    /// whether the value is filled from the blank value or not
    pub from_blank: bool,
    pub value: Value,
}
