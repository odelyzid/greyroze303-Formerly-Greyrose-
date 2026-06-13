use std::num::NonZeroU32;

use super::{
    container::Container, default::DefaultKIFieldValue, flags::FieldFlags, memory::MemoryStorage,
    string_ptr::StringPtr, ty::KIClassType,
};

#[derive(Debug, Clone)]
pub struct KIClassLayout {
    pub(crate) name: StringPtr,
    pub(crate) base: Option<NonZeroU32>,
    pub(crate) hash: u32,
    pub(crate) server_only: bool,
    pub(crate) fields: Vec<KIClassFieldLayout>,
}

impl KIClassLayout {
    pub fn name(&self) -> StringPtr {
        self.name
    }

    pub fn has_base(&self) -> bool {
        self.base.is_some()
    }

    pub fn base(&self) -> Option<u32> {
        self.base.map(|i| i.get())
    }

    pub fn hash_id(&self) -> u32 {
        self.hash
    }

    pub fn fields(&self) -> &[KIClassFieldLayout] {
        &self.fields
    }

    pub fn fields_with_flags(&self, flags: FieldFlags) -> FieldWithFlagsIter<'_> {
        FieldWithFlagsIter {
            fields: &self.fields,
            flags,
            index: 0,
        }
    }

    pub fn fields_without_flags(&self, flags: FieldFlags) -> FieldWithoutFlagsIter<'_> {
        FieldWithoutFlagsIter {
            fields: &self.fields,
            flags,
            index: 0,
        }
    }
}

/// An iterator over fields on a class layout that returns fields which contains certain flags.
/// This is the inverse of `FieldWithoutFlagsIter`.
pub struct FieldWithFlagsIter<'a> {
    fields: &'a [KIClassFieldLayout],
    flags: FieldFlags,
    index: usize,
}

impl<'a> Iterator for FieldWithFlagsIter<'a> {
    type Item = &'a KIClassFieldLayout;

    fn next(&mut self) -> Option<Self::Item> {
        while self.index < self.fields.len() {
            let field = &self.fields[self.index];
            self.index += 1;
            if field.flags.contains(self.flags) {
                return Some(field);
            }
        }
        None
    }
}

/// An iterator over fields on a class layout that returns fields which does not contain certain flags.
/// This is the inverse of `FieldWithFlagsIter`.
pub struct FieldWithoutFlagsIter<'a> {
    fields: &'a [KIClassFieldLayout],
    flags: FieldFlags,
    index: usize,
}

impl<'a> Iterator for FieldWithoutFlagsIter<'a> {
    type Item = &'a KIClassFieldLayout;

    fn next(&mut self) -> Option<Self::Item> {
        while self.index < self.fields.len() {
            let field = &self.fields[self.index];
            self.index += 1;
            if !field.flags.contains(self.flags) {
                return Some(field);
            }
        }
        None
    }
}

#[derive(Debug, Clone)]
pub struct KIClassFieldLayout {
    pub(crate) name: StringPtr,
    pub(crate) flags: FieldFlags,
    pub(crate) hash: u32,
    pub(crate) ty: KIClassType,
    pub(crate) default_value: Option<DefaultKIFieldValue>,
    pub(crate) memory_storage: MemoryStorage,
    pub(crate) container: Container,
    pub(crate) server_only: bool,
}

impl KIClassFieldLayout {
    pub fn name(&self) -> StringPtr {
        self.name
    }

    pub fn flags(&self) -> FieldFlags {
        self.flags
    }

    pub fn hash(&self) -> u32 {
        self.hash
    }

    pub fn ty(&self) -> KIClassType {
        self.ty
    }

    pub fn memory_storage(&self) -> MemoryStorage {
        self.memory_storage
    }

    pub fn container(&self) -> Container {
        self.container
    }

    pub fn default_value(&self) -> Option<&DefaultKIFieldValue> {
        self.default_value.as_ref()
    }

    pub fn is_ptr(&self) -> bool {
        self.memory_storage != MemoryStorage::Value
    }
}

#[derive(Debug, Clone)]
pub struct KIEnumLayout {
    pub(crate) hash: u32,
    pub(crate) name: StringPtr,
    pub(crate) default_value: EnumValueData,
    pub(crate) value_list: Vec<(StringPtr, EnumValue)>,
}

#[derive(Debug, Clone)]
pub struct KIBitFlagsLayout {
    pub(crate) default_value: EnumValueData,
    pub(crate) value_list: Vec<(StringPtr, EnumValue)>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EnumValueData {
    U8(u8),
    I8(i8),
    U16(u16),
    I16(i16),
    U32(u32),
    I32(i32),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum EnumValue {
    Value(EnumValueData),
    Name(StringPtr),
}
