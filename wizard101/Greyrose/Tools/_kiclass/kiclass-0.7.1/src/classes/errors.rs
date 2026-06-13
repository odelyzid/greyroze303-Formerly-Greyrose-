#[derive(Debug, Clone, Copy)]
pub struct ParseContainerError;

impl std::fmt::Display for ParseContainerError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(
            f,
            "Attempted to parse a Container from str but it did not represent a valid value (Static, List, Vector)"
        )
    }
}

impl std::error::Error for ParseContainerError {}

#[derive(Debug, Clone, Copy)]
pub struct ParseClassTypeError;

impl std::fmt::Display for ParseClassTypeError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(
            f,
            "Attempted to parse a ClassType from str but it did not represent a valid value"
        )
    }
}

impl std::error::Error for ParseClassTypeError {}
