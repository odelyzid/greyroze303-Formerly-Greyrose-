use std::num::NonZeroU32;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct StringPtr {
    index: Option<NonZeroU32>,
    generation: u32,
}

impl StringPtr {
    pub fn new(index: Option<u32>, generation: u32) -> Self {
        if let Some(index) = index {
            Self {
                index: NonZeroU32::new(index.wrapping_add(1)),
                generation,
            }
        } else {
            Self {
                index: None,
                generation,
            }
        }
    }

    pub fn get_index(&self) -> u32 {
        if let Some(i) = self.index {
            i.get().wrapping_sub(1)
        } else {
            0xffffffff
        }
    }

    pub fn get(&self) -> Option<u32> {
        self.index.map(|i| i.get().wrapping_sub(1))
    }

    pub fn exists(&self) -> bool {
        self.index.is_some()
    }
}

impl bitstream_io::Primitive for StringPtr {
    type Bytes = [u8; 4];

    fn buffer() -> Self::Bytes {
        [0u8; 4]
    }

    fn to_be_bytes(self) -> Self::Bytes {
        if let Some(i) = self.index {
            i.get().wrapping_sub(1).to_be_bytes()
        } else {
            [0xffu8; 4]
        }
    }

    fn to_le_bytes(self) -> Self::Bytes {
        if let Some(i) = self.index {
            i.get().wrapping_sub(1).to_le_bytes()
        } else {
            [0xffu8; 4]
        }
    }

    fn from_be_bytes(bytes: Self::Bytes) -> Self {
        let bytes = u32::from_be_bytes(bytes);
        let index = NonZeroU32::new(bytes.wrapping_add(1));
        StringPtr { index, generation: 0 }
    }

    fn from_le_bytes(bytes: Self::Bytes) -> Self {
        let bytes = u32::from_le_bytes(bytes);
        let index = NonZeroU32::new(bytes.wrapping_add(1));
        StringPtr { index, generation: 0 }
    }
}