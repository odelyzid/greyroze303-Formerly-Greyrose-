use std::str::FromStr;

use super::errors::ParseContainerError;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Container {
    Single,
    Vector,
    List,
}

impl FromStr for Container {
    type Err = ParseContainerError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        Ok(match s {
            "Static" => Self::Single,
            "List" => Self::List,
            "Vector" => Self::Vector,
            _ => return Err(ParseContainerError),
        })
    }
}
