use bitflags::bitflags;
use bitstream_io::Primitive;

bitflags! {
    #[derive(Debug, Clone, Copy, PartialEq, Eq)]
    #[pyo3::pyclass]
    pub struct FieldFlags : u32 {
        const SAVE = 0x00000001;
        const COPY = 0x00000002;
        const PUBLIC = 0x00000004;
        const TRANSMITPLAYER = 0x00000008;
        const TRANSMITCSR = 0x00000010;
        const PERSIST = 0x00000020;
        const DEPRECATED = 0x00000040;
        const NOSCRIPT = 0x00000080;
        const DELTA_SAVE = 0x00000100;
        const BINARY = 0x00000200;
        const DEFAULT = Self::SAVE.bits() | Self::COPY.bits() | Self::PUBLIC.bits();
        const TRANSMIT = Self::TRANSMITPLAYER.bits() | Self::TRANSMITCSR.bits();
        const NOEDIT = 0x00010000;
        const FILENAME = 0x00020000;
        const COLOR = 0x00040000;
        const RANGE = 0x00080000;
        const BITS = 0x00100000;
        const ENUM = 0x00200000;
        const LOCALIZED = 0x00400000;
        const STRINGKEY = 0x00800000;
        const OBJECTID = 0x01000000;
        const REFERENCEID = 0x02000000;
        const RADIANS = 0x04000000;
        const NAME = 0x08000000;
        const NAMEREF = 0x10000000;
        const OVERRIDE = 0x20000000;
        const WEAK = 0x40000000;
        const EDITORMASK = 0xFFFF0000;
    }
}

impl Primitive for FieldFlags {
    type Bytes = [u8; 4];

    fn buffer() -> Self::Bytes {
        [0u8; 4]
    }

    fn to_be_bytes(self) -> Self::Bytes {
        self.bits().to_be_bytes()
    }

    fn to_le_bytes(self) -> Self::Bytes {
        self.bits().to_le_bytes()
    }

    fn from_be_bytes(bytes: Self::Bytes) -> Self {
        FieldFlags::from_bits_retain(u32::from_be_bytes(bytes))
    }

    fn from_le_bytes(bytes: Self::Bytes) -> Self {
        FieldFlags::from_bits_retain(u32::from_le_bytes(bytes))
    }
}
