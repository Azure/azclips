mod help;
mod prompt;
mod value;

pub use help::ArgHelpOp;
pub use prompt::ArgPromptInputOp;
pub use value::ArgValueAssignOp;

#[derive(Debug, PartialEq)]
pub enum ArgOp {
    Prompt(ArgPromptInputOp),
    Value(ArgValueAssignOp),
    Help(ArgHelpOp),
}
