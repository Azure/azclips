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

impl ArgOp {
    pub fn with_append_flag(&mut self, is_append: bool) -> &mut Self {
        match self {
            ArgOp::Prompt(op) => op.is_append = is_append,
            ArgOp::Value(op) => op.is_append = is_append,
            ArgOp::Help(_) => {}
        }
        self
    }
}
