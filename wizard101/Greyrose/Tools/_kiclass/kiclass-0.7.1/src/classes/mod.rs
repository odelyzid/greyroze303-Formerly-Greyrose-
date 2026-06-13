use std::sync::Arc;

use ahash::AHashMap;
use indexmap::IndexSet;
use layout::{KIBitFlagsLayout, KIClassLayout, KIEnumLayout};
use string_ptr::StringPtr;

use crate::{hashing::hash_string, python_wrappers::KIClassMode};

pub mod builtins;
pub mod container;
pub mod default;
pub mod errors;
pub mod flags;
pub mod layout;
pub mod memory;
pub mod string_ptr;
pub mod ty;
pub mod xml;

#[derive(Debug, Clone)]
pub struct StringTable {
    strings: IndexSet<String>,
    generation: u32,
}

impl StringTable {
    fn new() -> Self {
        Self {
            strings: IndexSet::new(),
            generation: 0,
        }
    }

    fn with_capacity(capacity: usize) -> Self {
        Self {
            strings: IndexSet::with_capacity(capacity),
            generation: 0,
        }
    }

    fn get(&self, string_ptr: StringPtr) -> Option<&str> {
        let index = string_ptr.get_index() as usize;
        if index == 0xffffffff {
            None
        } else {
            self.strings.get_index(index).map(|s| s.as_str())
        }
    }

    fn get_index_of(&self, string: &str) -> Option<usize> {
        self.strings.get_index_of(string)
    }

    fn get_ptr_of(&self, string: &str) -> Option<StringPtr> {
        self.strings.get_index_of(string).map(|i| StringPtr::new(Some(i as u32), self.generation))
    }

    fn insert(&mut self, string: String) -> StringPtr {
        let index = self.strings.insert_full(string).0;
        StringPtr::new(Some(index as u32), self.generation)
    }

    fn len(&self) -> usize {
        self.strings.len()
    }

    fn generation(&self) -> u32 {
        self.generation
    }

    fn increment_generation(&mut self) {
        self.generation = self.generation.wrapping_add(1);
    }

    fn remove_string(&mut self, string: &str) -> bool {
        if self.strings.swap_remove(string) {
            self.increment_generation();
            true
        } else {
            false
        }
    }

    fn remove(&mut self, string_ptr: StringPtr) -> bool {
        if self.strings.swap_remove_index(string_ptr.get_index() as usize).is_some() {
            self.increment_generation();
            true
        } else {
            false
        }
    }

    fn clear(&mut self) {
        self.strings.clear();
        self.increment_generation();
    }

    fn contains(&self, string: &str) -> bool {
        self.strings.contains(string)
    }

    fn contains_ptr(&self, string_ptr: StringPtr) -> bool {
        self.strings.len() >= string_ptr.get_index() as usize
    }

    fn iter(&self) -> indexmap::set::Iter<String> {
        self.strings.iter()
    }
}

#[derive(Debug)]
pub struct ClassData {
    /// The string table used for class, enum, and bitflags names, as well as other strings.
    /// The index of the string in this table is used as a pointer in the class, enum, and bitflags layouts; as such it is unsafe to remove strings from this table once they have been added, as it may invalidate existing pointers.
    strings: StringTable,
    classes: AHashMap<u32, Arc<KIClassLayout>>,
    enums: AHashMap<u32, Arc<KIEnumLayout>>,
    bitflags: Vec<Arc<KIBitFlagsLayout>>,
    mode: KIClassMode,
}

impl ClassData {
    pub fn classes(&self) -> std::collections::hash_map::Values<'_, u32, Arc<KIClassLayout>> {
        self.classes.values()
    }

    pub fn enums(&self) -> std::collections::hash_map::Values<'_, u32, Arc<KIEnumLayout>> {
        self.enums.values()
    }

    pub fn bitflags(&self) -> &[Arc<KIBitFlagsLayout>] {
        &self.bitflags
    }

    pub fn mode(&self) -> KIClassMode {
        self.mode
    }

    pub fn get_enum(&self, name: &str) -> Option<&Arc<KIEnumLayout>> {
        let hash = hash_string(name.as_bytes());
        self.get_enum_by_hash(hash)
    }

    pub fn get_bitflags(&self, index: usize) -> Option<&Arc<KIBitFlagsLayout>> {
        self.bitflags.get(index)
    }

    pub fn get_class(&self, name: &str) -> Option<&Arc<KIClassLayout>> {
        let hash = hash_string(name.as_bytes());
        self.get_class_by_hash(hash)
    }

    pub fn get_enum_by_hash(&self, hash: u32) -> Option<&Arc<KIEnumLayout>> {
        self.enums.get(&hash)
    }

    pub fn get_class_by_hash(&self, hash: u32) -> Option<&Arc<KIClassLayout>> {
        self.classes.get(&hash)
    }

    pub fn get_string(&self, string_ptr: StringPtr) -> Option<&str> {
        if let Some(v) = self.strings.get(string_ptr) {
            Some(v)
        } else {
            None
        }
    }

    pub fn find_string(&self, string: &str) -> StringPtr {
        let index = self.strings.get_index_of(string);
        StringPtr::new(index.map(|i| i as u32), self.strings.generation())
    }

    pub fn get_or_add_string<S: Into<String> + AsRef<str>>(&mut self, value: S) -> StringPtr {
        if let Some(i) = self.strings.get_index_of(value.as_ref()) {
            StringPtr::new(Some(i as u32), self.strings.generation())
        } else {
            let index = StringPtr::new(Some(self.strings.len() as u32), self.strings.generation());
            self.strings.insert(value.into());
            index
        }
    }
}
