use std::io::{Read, Write};

use crate::{
    argparse::{parse_arg_value_str, CrsArgWrapper, SHPartialKeys},
    error::{AzResult, AzUserUsageError},
    pb::CrsArgPromptInput,
};

use super::{ArgOp, ArgValueAssignOp};

#[derive(Debug, PartialEq)]
pub struct ArgPromptInputOp {
    pub(crate) keys: SHPartialKeys,
    pub(crate) is_append: bool,
    pub(crate) prompt: CrsArgPromptInput,
}

impl ArgPromptInputOp {
    pub fn arg_keys(&self) -> &SHPartialKeys {
        &self.keys
    }

    pub fn in_secret(&self) -> bool {
        self.prompt.secret
    }
}

impl ArgPromptInputOp {
    /// Prompt the user for input and return the value
    ///
    /// The space trim should be handled in the input reader
    pub fn prompt_input<R: Read, W: Write>(
        &self,
        arg: &CrsArgWrapper,
        input: &mut R,
        output: &mut W,
    ) -> AzResult<ArgValueAssignOp> {
        // Write prompt message to output
        writeln!(output, "{}:", self.prompt.prompt).map_err(AzUserUsageError::InvalidInputError)?;
        output
            .flush()
            .map_err(AzUserUsageError::InvalidInputError)?;

        // Read input value
        let mut buffer = String::new();
        input
            .read_to_string(&mut buffer)
            .map_err(AzUserUsageError::InvalidInputError)?;

        if self.prompt.confirm {
            writeln!(output, "Please input the value again:")
                .map_err(AzUserUsageError::InvalidInputError)?;
            output
                .flush()
                .map_err(AzUserUsageError::InvalidInputError)?;

            let mut buffer2 = String::new();
            input
                .read_to_string(&mut buffer2)
                .map_err(AzUserUsageError::InvalidInputError)?;

            if buffer != buffer2 {
                return Err(AzUserUsageError::InvalidArgumentValueError(
                    "Input values do not match".to_string(),
                )
                .into());
            }
        }

        let value = parse_arg_value_str(arg, Some(&buffer))?;
        let value = match value {
            ArgOp::Value(value) => value,
            _ => {
                return Err(AzUserUsageError::InvalidArgumentValueError(
                    "invalid argument value".to_string(),
                )
                .into())
            }
        };

        Ok(value)
    }
}
