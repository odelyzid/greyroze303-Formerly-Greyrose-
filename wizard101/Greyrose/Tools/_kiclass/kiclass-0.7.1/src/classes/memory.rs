#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MemoryStorage {
    Value,
    RawPointer,
    SharedPointer,
}
