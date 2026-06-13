use std::io::{BufWriter, Cursor, Seek, Write};
use std::sync::Arc;
use std::{fs::File, io::BufReader, num::NonZeroU32, path::Path, str::FromStr};

use crate::classes::StringTable;
use crate::classes::layout::{EnumValueData, KIBitFlagsLayout, KIEnumLayout};
use crate::classes::ty::ki_class_type_from_str;
use crate::hashing::{hash_string, light_hash_string};
use crate::python_wrappers::KIClassMode;
use ahash::AHashMap;
use bitstream_io::{ByteRead, ByteReader, ByteWrite, ByteWriter, LittleEndian};
use indexmap::IndexSet;
use log::{debug, error, warn};
use uuid::Uuid;
use xml::EventReader;
use xml::reader::XmlEvent;

use super::builtins::{
    Color, Matrix3x3, PointFloat, PointInt, PointUInt, RectFloat, RectInt, RectUInt, SizeFloat,
    SizeInt, SizeUInt, UUniqueID, Vector3D,
};
use super::container::Container;
use super::default::DefaultKIFieldValue;
use super::flags::FieldFlags;

use super::layout::{EnumValue, KIClassFieldLayout};
use super::memory::MemoryStorage;
use super::ty::KIClassType;
use super::{ClassData, layout::KIClassLayout, string_ptr::StringPtr};

pub struct XMLtoClassBin;

impl XMLtoClassBin {
    pub fn convert_from_xml(path: impl AsRef<Path>, to: impl AsRef<Path>, mode: KIClassMode) -> std::io::Result<()> {
        let data = Self::load_classes_from_xml_file(path, mode);
        Self::save_to_file(data, to.as_ref())
    }

    pub fn load_from_file(path: impl AsRef<Path>) -> std::io::Result<ClassData> {
        let mut output = ClassData {
            strings: StringTable::with_capacity(0),
            classes: AHashMap::with_capacity(0),
            enums: AHashMap::with_capacity(0),
            bitflags: Vec::with_capacity(0),
            mode: KIClassMode::Wizard, //Default value, will be overwritten by actual value in file
        };

        let file = BufReader::new(File::open(path)?);
        let mut rdr = ByteReader::endian(file, LittleEndian);
        let signature = rdr.read::<[u8; 5]>()?;
        if &signature != b"kicdb" {
            return Err(std::io::Error::from(std::io::ErrorKind::InvalidData));
        }
        let mode = match rdr.read::<u8>()? {
            0 => KIClassMode::Wizard,
            1 => KIClassMode::Pirate,
            v => {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("Found a invalid mode with id {v} at offset: {}", rdr.reader().stream_position()?),
                ));
            }
        };
        output.mode = mode;
        let string_amount = rdr.read::<u32>()?;
        output.strings = StringTable::with_capacity(string_amount as usize);
        for _ in 0..string_amount {
            let string_length = rdr.read::<u8>()?;
            let s = rdr.read_to_vec(string_length as usize)?;
            let s = unsafe { String::from_utf8_unchecked(s) };
            output.strings.insert(s);
        }
        let class_amount = rdr.read::<u32>()?;
        output.classes = AHashMap::with_capacity(class_amount as usize);
        for _ in 0..class_amount {
            let mut class = KIClassLayout {
                name: StringPtr::new(None, 0),
                base: None,
                hash: 0,
                server_only: false,
                fields: Vec::with_capacity(0),
            };
            let class_hash = rdr.read::<u32>()?;
            class.hash = class_hash;
            let class_name = rdr.read::<StringPtr>()?;
            class.name = class_name;
            let base = rdr.read::<u32>()?;
            class.base = NonZeroU32::new(base);
            let server_only = rdr.read::<u8>()? > 0;
            class.server_only = server_only;
            let field_len = rdr.read::<u8>()?;
            class.fields = Vec::with_capacity(field_len as usize);
            for _ in 0..field_len {
                let hash = rdr.read::<u32>()?;
                let name = rdr.read::<StringPtr>()?;
                debug!("Reading field: {:?} of class {:?} at offset: {}", output.get_string(name), output.get_string(class.name), rdr.reader().stream_position()?);
                let flags = rdr.read::<FieldFlags>()?;
                let server_only = rdr.read::<u8>()? > 0;
                let ty = rdr.read::<u8>()?;
                let ty: KIClassType = match ty {
                    0 => {
                        warn!("Found a field with unknown type at offset: {}", rdr.reader().stream_position()?);
                        KIClassType::Unknown
                    }
                    1 => KIClassType::U8,
                    2 => KIClassType::I8,
                    3 => KIClassType::U16,
                    4 => KIClassType::I16,
                    5 => KIClassType::U32,
                    6 => KIClassType::I32,
                    7 => KIClassType::U64,
                    8 => KIClassType::I64,
                    9 => KIClassType::F32,
                    10 => KIClassType::F64,
                    11 => KIClassType::Bool,
                    12 => KIClassType::String,
                    13 => KIClassType::WString,
                    14 => KIClassType::Gid,
                    15 => KIClassType::BitInt(rdr.read()?),
                    16 => KIClassType::Vector3D,
                    17 => KIClassType::PointFloat,
                    18 => KIClassType::PointInt,
                    19 => KIClassType::PointUInt,
                    20 => KIClassType::Matrix3x3,
                    21 => KIClassType::SizeInt,
                    22 => KIClassType::SizeFloat,
                    23 => KIClassType::SizeUInt,
                    24 => KIClassType::UUniqueID,
                    25 => KIClassType::Color,
                    26 => KIClassType::Enum(rdr.read()?),
                    27 => KIClassType::BitFlags(rdr.read()?),
                    28 => KIClassType::Class(rdr.read()?),
                    29 => {
                        let key_id = rdr.read()?;
                        let value_id = rdr.read()?;
                        KIClassType::SerializeMap(key_id, value_id)
                    }
                    30 => {
                        let first_id = rdr.read()?;
                        let second_id = rdr.read()?;
                        KIClassType::SerializePair(first_id, second_id)
                    }
                    31 => KIClassType::PirateNameIndices,
                    32 => KIClassType::RectFloat,
                    33 => KIClassType::RectInt,
                    34 => KIClassType::RectUInt,
                    v => {
                        return Err(std::io::Error::new(
                            std::io::ErrorKind::InvalidData,
                            format!("Found a unsupported type with id {v} at offset: {}", rdr.reader().stream_position()?),
                        ));
                    }
                };
                debug!("Expected type of field {:?} is {:?} at offset: {}", output.get_string(name), ty, rdr.reader().stream_position()?);
                let container = match rdr.read::<u8>()? {
                    0 => Container::Single,
                    1 => Container::Vector,
                    2 => Container::List,
                    v => {
                        return Err(std::io::Error::new(
                            std::io::ErrorKind::InvalidData,
                            format!("Found a invalid container with id {v} at offset: {}", rdr.reader().stream_position()?),
                        ));
                    }
                };
                let memory_storage = match rdr.read::<u8>()? {
                    0 => MemoryStorage::Value,
                    1 => MemoryStorage::RawPointer,
                    2 => MemoryStorage::SharedPointer,
                    v => {
                        return Err(std::io::Error::new(
                            std::io::ErrorKind::InvalidData,
                            format!("[{:?}], Found a invalid memory storage with id {v} at offset: {}, with expected type: {:?}", output.get_string(name), rdr.reader().stream_position()?, {ty}),
                        ));
                    }
                };
                let has_default = rdr.read::<u8>()? > 0;
                let default_value = if !has_default {
                    None
                } else {
                    match ty {
                        KIClassType::Unknown => {
                            debug!("Found a field with unknown type but has default value at offset: {}. Skipping default value as it's impossible to read without knowing the type", rdr.reader().stream_position()?);
                            //SKIP as default value is impossible
                            None
                        }
                        KIClassType::U8 => Some(DefaultKIFieldValue::U8(rdr.read()?)),
                        KIClassType::I8 => Some(DefaultKIFieldValue::I8(rdr.read()?)),
                        KIClassType::U16 => Some(DefaultKIFieldValue::U16(rdr.read()?)),
                        KIClassType::I16 => Some(DefaultKIFieldValue::I16(rdr.read()?)),
                        KIClassType::U32 => Some(DefaultKIFieldValue::U32(rdr.read()?)),
                        KIClassType::I32 => Some(DefaultKIFieldValue::I32(rdr.read()?)),
                        KIClassType::U64 => Some(DefaultKIFieldValue::U64(rdr.read()?)),
                        KIClassType::I64 => Some(DefaultKIFieldValue::I64(rdr.read()?)),
                        KIClassType::F32 => Some(DefaultKIFieldValue::F32(rdr.read()?)),
                        KIClassType::F64 => Some(DefaultKIFieldValue::F64(rdr.read()?)),
                        KIClassType::Bool => Some(DefaultKIFieldValue::Bool(rdr.read::<u8>()? > 0)),
                        KIClassType::String => {
                            let s_len = rdr.read::<u8>()?;
                            let s = rdr.read_to_vec(s_len as usize)?;
                            Some(DefaultKIFieldValue::String(unsafe {
                                String::from_utf8_unchecked(s)
                            }))
                        }
                        KIClassType::WString => {
                            let s_len = rdr.read::<u8>()?;
                            let mut s = Vec::with_capacity(s_len as usize);
                            for _ in 0..s_len {
                                s.push(rdr.read::<u16>()?);
                            }
                            let s = String::from_utf16(&s).expect("Found invalid utf16 in wstring");
                            Some(DefaultKIFieldValue::WString(s))
                        }
                        KIClassType::Gid => Some(DefaultKIFieldValue::Gid(rdr.read()?)),
                        KIClassType::BitInt(_) => Some(DefaultKIFieldValue::BitInt(rdr.read()?)),
                        KIClassType::Vector3D => {
                            let x = rdr.read::<f32>()?;
                            let y = rdr.read::<f32>()?;
                            let z = rdr.read::<f32>()?;
                            Some(DefaultKIFieldValue::Vector3D(Vector3D { x, y, z }))
                        }
                        KIClassType::PointFloat => {
                            let x = rdr.read::<f32>()?;
                            let y = rdr.read::<f32>()?;
                            Some(DefaultKIFieldValue::PointFloat(PointFloat { x, y }))
                        }
                        KIClassType::PointInt => {
                            let x = rdr.read::<i32>()?;
                            let y = rdr.read::<i32>()?;
                            Some(DefaultKIFieldValue::PointInt(PointInt { x, y }))
                        }
                        KIClassType::PointUInt => {
                            let x = rdr.read::<u32>()?;
                            let y = rdr.read::<u32>()?;
                            Some(DefaultKIFieldValue::PointUInt(PointUInt { x, y }))
                        }
                        KIClassType::RectFloat => {
                            let left = rdr.read::<f32>()?;
                            let top = rdr.read::<f32>()?;
                            let right = rdr.read::<f32>()?;
                            let bottom = rdr.read::<f32>()?;
                            Some(DefaultKIFieldValue::RectFloat(RectFloat {
                                left,
                                top,
                                right,
                                bottom,
                            }))
                        }
                        KIClassType::RectInt => {
                            let left = rdr.read::<i32>()?;
                            let top = rdr.read::<i32>()?;
                            let right = rdr.read::<i32>()?;
                            let bottom = rdr.read::<i32>()?;
                            Some(DefaultKIFieldValue::RectInt(RectInt {
                                left,
                                top,
                                right,
                                bottom,
                            }))
                        }
                        KIClassType::RectUInt => {
                            let left = rdr.read::<u32>()?;
                            let top = rdr.read::<u32>()?;
                            let right = rdr.read::<u32>()?;
                            let bottom = rdr.read::<u32>()?;
                            Some(DefaultKIFieldValue::RectUInt(RectUInt {
                                left,
                                top,
                                right,
                                bottom,
                            }))
                        }
                        KIClassType::Matrix3x3 => {
                            let x1 = rdr.read::<f32>()?;
                            let y1 = rdr.read::<f32>()?;
                            let z1 = rdr.read::<f32>()?;
                            let x2 = rdr.read::<f32>()?;
                            let y2 = rdr.read::<f32>()?;
                            let z2 = rdr.read::<f32>()?;
                            let x3 = rdr.read::<f32>()?;
                            let y3 = rdr.read::<f32>()?;
                            let z3 = rdr.read::<f32>()?;
                            Some(DefaultKIFieldValue::Matrix3x3(Matrix3x3 {
                                x1,
                                y1,
                                z1,
                                x2,
                                y2,
                                z2,
                                x3,
                                y3,
                                z3,
                            }))
                        }
                        KIClassType::SizeFloat => {
                            let x = rdr.read::<f32>()?;
                            let y = rdr.read::<f32>()?;
                            Some(DefaultKIFieldValue::SizeFloat(SizeFloat { x, y }))
                        }
                        KIClassType::SizeInt => {
                            let x = rdr.read::<i32>()?;
                            let y = rdr.read::<i32>()?;
                            Some(DefaultKIFieldValue::SizeInt(SizeInt { x, y }))
                        }
                        KIClassType::SizeUInt => {
                            let x = rdr.read::<u32>()?;
                            let y = rdr.read::<u32>()?;
                            Some(DefaultKIFieldValue::SizeUInt(SizeUInt { x, y }))
                        }
                        KIClassType::UUniqueID => {
                            let guid = rdr.read::<u128>()?;
                            Some(DefaultKIFieldValue::UUniqueID(UUniqueID {
                                inner: Uuid::from_u128_le(guid),
                            }))
                        }
                        KIClassType::Color => {
                            let r = rdr.read::<u8>()?;
                            let g = rdr.read::<u8>()?;
                            let b = rdr.read::<u8>()?;
                            let a = rdr.read::<u8>()?;
                            Some(DefaultKIFieldValue::Color(Color { r, g, b, a }))
                        }
                        KIClassType::Enum(_) => {
                            Some(DefaultKIFieldValue::Enum(EnumValueData::I32(rdr.read()?)))
                        }
                        KIClassType::BitFlags(_) => {
                            Some(DefaultKIFieldValue::Enum(EnumValueData::I32(rdr.read()?)))
                        }
                        KIClassType::Class(_) => None,
                        KIClassType:: SerializeMap(_, _) | KIClassType::SerializePair(_, _) | KIClassType::PirateNameIndices => {
                            //TODO
                            None
                        }
                    }
                };
                class.fields.push(KIClassFieldLayout {
                    name,
                    hash,
                    flags,
                    container,
                    ty,
                    default_value,
                    memory_storage,
                    server_only,
                })
            }
            output.classes.insert(class.hash, class.into());
        }
        let enum_amount = rdr.read::<u32>()?;
        output.enums = AHashMap::with_capacity(enum_amount as usize);
        for _ in 0..enum_amount {
            let hash = rdr.read::<u32>()?;
            let name = rdr.read::<StringPtr>()?;
            let enum_inner = rdr.read::<u8>()?;
            let default_value = match enum_inner {
                0 => {
                    EnumValueData::U8(rdr.read::<u32>()? as u8)
                },
                1 => {
                    EnumValueData::I8(rdr.read::<i32>()? as i8)
                },
                2 => {
                    EnumValueData::U16(rdr.read::<u32>()? as u16)
                },
                3 => {
                    EnumValueData::I16(rdr.read::<i32>()? as i16)
                },
                4 => {
                    EnumValueData::U32(rdr.read()?)
                },
                5 => {
                    EnumValueData::I32(rdr.read()?)
                },
                _ => return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("Found a unsupported enum inner type with id {enum_inner} at offset: {}", rdr.reader().stream_position()?),
                )),
            };
            let value_amount = rdr.read::<u16>()?;
            let mut value_list = Vec::with_capacity(value_amount as usize);
            for _ in 0..value_amount {
                let name = rdr.read::<StringPtr>()?;
                match enum_inner {
                    0 => {
                        let value = rdr.read::<u32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::U8(value as u8))));
                    },
                    1 => {
                        let value = rdr.read::<i32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::I8(value as i8))));
                    },
                    2 => {
                        let value = rdr.read::<u32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::U16(value as u16))));
                    },
                    3 => {
                        let value = rdr.read::<i32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::I16(value as i16))));
                    },
                    4 => {
                        let value = rdr.read::<u32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::U32(value))));
                    },
                    5 => {
                        let value = rdr.read::<i32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::I32(value))));
                    },
                    _ => return Err(std::io::Error::new(
                        std::io::ErrorKind::InvalidData,
                        format!("Found a unsupported enum inner type with id {enum_inner} at offset: {}", rdr.reader().stream_position()?),
                    )),
                }
            }
            let e = KIEnumLayout {
                hash,
                name,
                default_value,
                value_list,
            }
            .into();
            output.enums.insert(hash, e);
        }

        let nameless_enum_amount = rdr.read::<u32>()?;
        output.bitflags = Vec::with_capacity(nameless_enum_amount as usize);
        for _ in 0..nameless_enum_amount {
            let enum_inner = rdr.read::<u8>()?;
            let default_value = match enum_inner {
                0 => EnumValueData::U8(rdr.read::<u32>()? as u8),
                1 => EnumValueData::I8(rdr.read::<i32>()? as i8),
                2 => EnumValueData::U16(rdr.read::<u32>()? as u16),
                3 => EnumValueData::I16(rdr.read::<i32>()? as i16),
                4 => EnumValueData::U32(rdr.read::<u32>()?),
                5 => EnumValueData::I32(rdr.read::<i32>()?),
                _ => return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("Found a unsupported bitflags inner type with id {enum_inner} at offset: {}", rdr.reader().stream_position()?),
                )),
            };
            let value_amount = rdr.read::<u16>()?;
            let mut value_list = Vec::with_capacity(value_amount as usize);
            for _ in 0..value_amount {
                let name = rdr.read::<StringPtr>()?;
                match enum_inner {
                    0 => {
                        let value = rdr.read::<u32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::U8(value as u8))));
                    },
                    1 => {
                        let value = rdr.read::<i32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::I8(value as i8))));
                    },
                    2 => {
                        let value = rdr.read::<u32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::U16(value as u16))));
                    },
                    3 => {
                        let value = rdr.read::<i32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::I16(value as i16))));
                    },
                    4 => {
                        let value = rdr.read::<u32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::U32(value))));
                    },
                    5 => {
                        let value = rdr.read::<i32>()?;
                        value_list.push((name, EnumValue::Value(EnumValueData::I32(value))));
                    },
                    _ => return Err(std::io::Error::new(
                        std::io::ErrorKind::InvalidData,
                        format!("Found a unsupported bitflags inner type with id {enum_inner} at offset: {}", rdr.reader().stream_position()?),
                    )),
                }
            }
            let e = KIBitFlagsLayout {
                default_value,
                value_list,
            }
            .into();
            output.bitflags.push(e);
        }

        Ok(output)
    }

    fn save_to_file(data: ClassData, path: impl AsRef<Path>) -> std::io::Result<()> {
        if std::fs::exists(&path)? {
            if let Ok(meta) = std::fs::metadata(&path) {
                if meta.is_dir() {
                    error!("A directory is not a valid output path: {:?}.", path.as_ref());
                    return Err(std::io::Error::from(std::io::ErrorKind::InvalidFilename));
                }
                if meta.is_file() {
                    error!("File already exists at {:?}. Not overwriting.", path.as_ref());
                    return Err(std::io::Error::from(std::io::ErrorKind::AlreadyExists));
                }
            }
        }
        let output = BufWriter::new(Cursor::new(Vec::with_capacity(1024 * 1024)));
        let mut output = ByteWriter::endian(output, LittleEndian);
        output.write_bytes(b"kicdb")?;
        output.write(match data.mode() {
            KIClassMode::Wizard => 0u8,
            KIClassMode::Pirate => 1u8,
        })?;
        output.write(data.strings.len() as u32)?;
        for s in data.strings.iter() {
            let d = s.as_bytes();
            if d.len() > u8::MAX as usize {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("String too large to fit in string table at offset: {}", output.writer().stream_position()?),
                ));
            }
            output.write(d.len() as u8)?;
            output.write_bytes(d)?;
        }
        output.write(data.classes.len() as u32)?;
        for class in data.classes() {
            output.write(class.hash)?;
            output.write(class.name)?;
            output.write(class.base.map(|i| i.get()).unwrap_or_default())?;
            output.write(class.server_only as u8)?;
            if class.fields.len() > u8::MAX as usize {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("Class has too many fields at offset: {}", output.writer().stream_position()?),
                ));
            }
            output.write(class.fields.len() as u8)?;
            for field in class.fields.iter() {
                output.write(field.hash)?;
                output.write(field.name)?;
                output.write(field.flags)?;
                output.write::<u8>(field.server_only as u8)?;
                match field.ty {
                    KIClassType::Unknown => {
                        debug!("Found a field with unknown type: {:?}. Saving as 0 at {}", data.get_string(field.name()), output.writer().stream_position()?);
                        output.write(0u8)?;
                    }
                    KIClassType::BitInt(v) => {
                        output.write(field.ty.tag())?;
                        output.write(v)?;
                    }
                    KIClassType::Enum(v) => {
                        output.write(field.ty.tag())?;
                        output.write(v)?;
                    }
                    KIClassType::BitFlags(v) => {
                        output.write(field.ty.tag())?;
                        output.write(v)?;
                    }
                    KIClassType::Class(v) => {
                        output.write(field.ty.tag())?;
                        output.write(v)?;
                    }
                    KIClassType::SerializeMap(key_id, value_id) => {
                        output.write(field.ty.tag())?;
                        output.write(key_id)?;
                        output.write(value_id)?;
                    }
                    KIClassType::SerializePair(first_id, second_id) => {
                        output.write(field.ty.tag())?;
                        output.write(first_id)?;
                        output.write(second_id)?;
                    }
                    _ => output.write(field.ty.tag())?,
                }
                output.write::<u8>(match field.container {
                    Container::Single => 0,
                    Container::Vector => 1,
                    Container::List => 2,
                })?;
                output.write::<u8>(match field.memory_storage {
                    MemoryStorage::Value => 0,
                    MemoryStorage::RawPointer => 1,
                    MemoryStorage::SharedPointer => 2,
                })?;
                if let Some(v) = &field.default_value {
                    output.write(1u8)?;
                    match v {
                        DefaultKIFieldValue::U8(v) => output.write(*v)?,
                        DefaultKIFieldValue::I8(v) => output.write(*v)?,
                        DefaultKIFieldValue::U16(v) => output.write(*v)?,
                        DefaultKIFieldValue::I16(v) => output.write(*v)?,
                        DefaultKIFieldValue::U32(v) => output.write(*v)?,
                        DefaultKIFieldValue::I32(v) => output.write(*v)?,
                        DefaultKIFieldValue::U64(v) => output.write(*v)?,
                        DefaultKIFieldValue::I64(v) => output.write(*v)?,
                        DefaultKIFieldValue::F32(v) => output.write(*v)?,
                        DefaultKIFieldValue::F64(v) => output.write(*v)?,
                        DefaultKIFieldValue::Bool(v) => output.write::<u8>(if *v { 1 } else { 0 })?,
                        DefaultKIFieldValue::String(v) => {
                            let v = v.as_bytes();
                            if v.len() > u8::MAX as usize {
                                return Err(std::io::Error::new(
                                    std::io::ErrorKind::InvalidData,
                                    format!("Default string is too long at offset: {}", output.writer().stream_position()?),
                                ));
                            }
                            output.write(v.len() as u8)?;
                            output.write_bytes(v)?;
                        }
                        DefaultKIFieldValue::WString(v) => {
                            let v = v.encode_utf16().collect::<Box<[u16]>>();
                            if v.len() > u8::MAX as usize {
                                return Err(std::io::Error::new(
                                    std::io::ErrorKind::InvalidData,
                                    format!("Default wstring is too long at offset: {}", output.writer().stream_position()?),
                                ));
                            }
                            output.write(v.len() as u8)?;
                            for v in v {
                                let v = v.to_le_bytes();
                                output.write_bytes(&v)?;
                            }
                        }
                        DefaultKIFieldValue::BitInt(v) => output.write(*v)?,
                        DefaultKIFieldValue::Gid(v) => output.write(*v)?,
                        DefaultKIFieldValue::Vector3D(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                            output.write(v.z)?;
                        }
                        DefaultKIFieldValue::PointFloat(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                        }
                        DefaultKIFieldValue::PointInt(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                        }
                        DefaultKIFieldValue::PointUInt(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                        }
                        DefaultKIFieldValue::RectFloat(v) => {
                            output.write(v.left)?;
                            output.write(v.top)?;
                            output.write(v.right)?;
                            output.write(v.bottom)?;
                        }
                        DefaultKIFieldValue::RectInt(v) => {
                            output.write(v.left)?;
                            output.write(v.top)?;
                            output.write(v.right)?;
                            output.write(v.bottom)?;
                        }
                        DefaultKIFieldValue::RectUInt(v) => {
                            output.write(v.left)?;
                            output.write(v.top)?;
                            output.write(v.right)?;
                            output.write(v.bottom)?;
                        }
                        DefaultKIFieldValue::Matrix3x3(v) => {
                            output.write(v.x1)?;
                            output.write(v.y1)?;
                            output.write(v.z1)?;
                            output.write(v.x2)?;
                            output.write(v.y2)?;
                            output.write(v.z2)?;
                            output.write(v.x3)?;
                            output.write(v.y3)?;
                            output.write(v.z3)?;
                        }
                        DefaultKIFieldValue::SizeFloat(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                        }
                        DefaultKIFieldValue::SizeInt(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                        }
                        DefaultKIFieldValue::SizeUInt(v) => {
                            output.write(v.x)?;
                            output.write(v.y)?;
                        }
                        DefaultKIFieldValue::UUniqueID(guid) => {
                            output.write::<u128>(guid.inner.as_u128().to_le())?;
                        }
                        DefaultKIFieldValue::Color(v) => {
                            output.write(v.r)?;
                            output.write(v.g)?;
                            output.write(v.b)?;
                            output.write(v.a)?;
                        }
                        DefaultKIFieldValue::Enum(v) => match v {
                            EnumValueData::U8(v) => output.write(*v as u32)?,
                            EnumValueData::I8(v) => output.write(*v as i32)?,
                            EnumValueData::U16(v) => output.write(*v as u32)?,
                            EnumValueData::I16(v) => output.write(*v as i32)?,
                            EnumValueData::U32(v) => output.write(*v)?,
                            EnumValueData::I32(v) => output.write(*v)?,
                        },
                    }
                } else {
                    output.write(0u8)?;
                }
            }
        }
        output.write(data.enums.len() as u32)?;
        for e in data.enums() {
            output.write(e.hash)?;
            output.write(e.name)?;
            match e.default_value {
                EnumValueData::U8(v) => {
                    output.write(0u8)?;
                    output.write(v as u32)?
                }
                EnumValueData::I8(v) => {
                    output.write(1u8)?;
                    output.write(v as i32)?
                }
                EnumValueData::U16(v) => {
                    output.write(2u8)?;
                    output.write(v as u32)?
                }
                EnumValueData::I16(v) => {
                    output.write(3u8)?;
                    output.write(v as i32)?
                }
                EnumValueData::U32(v) => {
                    output.write(4u8)?;
                    output.write(v)?
                }
                EnumValueData::I32(v) => {
                    output.write(5u8)?;
                    output.write(v)?
                }
            }
            if e.value_list.len() > u16::MAX as usize {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("Enum value list too large for enum {:?} at offset: {}", data.get_string(e.name), output.writer().stream_position()?),
                ));
            }
            output.write(e.value_list.len() as u16)?;
            for v in e.value_list.iter() {
                output.write(v.0)?;
                match v.1 {
                    EnumValue::Value(v) => match v {
                        EnumValueData::U8(v) => output.write(v as u32)?,
                        EnumValueData::I8(v) => output.write(v as i32)?,
                        EnumValueData::U16(v) => output.write(v as u32)?,
                        EnumValueData::I16(v) => output.write(v as i32)?,
                        EnumValueData::U32(v) => output.write(v)?,
                        EnumValueData::I32(v) => output.write(v)?,
                    },
                    EnumValue::Name(n) => {
                        let o = find_absolute_enum_value(e, n);
                        if let Some(o) = o {
                            match o {
                                EnumValueData::U8(v) => output.write(v as u32)?,
                                EnumValueData::I8(v) => output.write(v as i32)?,
                                EnumValueData::U16(v) => output.write(v as u32)?,
                                EnumValueData::I16(v) => output.write(v as i32)?,
                                EnumValueData::U32(v) => output.write(v)?,
                                EnumValueData::I32(v) => output.write(v)?,
                            }
                        } else {
                            return Err(std::io::Error::new(
                                std::io::ErrorKind::InvalidData,
                                format!("No matches found for enum value {:?} at offset: {}", data.get_string(n), output.writer().stream_position()?),
                            ));
                        }
                    }
                }
            }
        }
        output.write(data.bitflags.len() as u32)?;
        for e in data.bitflags() {
            match e.default_value {
                EnumValueData::U8(v) => {
                    output.write(0u8)?;
                    output.write(v as u32)?
                }
                EnumValueData::I8(v) => {
                    output.write(1u8)?;
                    output.write(v as i32)?
                }
                EnumValueData::U16(v) => {
                    output.write(2u8)?;
                    output.write(v as u32)?
                }
                EnumValueData::I16(v) => {
                    output.write(3u8)?;
                    output.write(v as i32)?
                }
                EnumValueData::U32(v) => {
                    output.write(4u8)?;
                    output.write(v)?
                }
                EnumValueData::I32(v) => {
                    output.write(5u8)?;
                    output.write(v)?
                }
            }
            if e.value_list.len() > u16::MAX as usize {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    format!("Enum value list too large for enum at offset: {}", output.writer().stream_position()?),
                ));
            }
            output.write(e.value_list.len() as u16)?;
            for v in e.value_list.iter() {
                output.write(v.0)?;
                match v.1 {
                    EnumValue::Value(v) => match v {
                        EnumValueData::U8(v) => output.write(v as u32)?,
                        EnumValueData::I8(v) => output.write(v as i32)?,
                        EnumValueData::U16(v) => output.write(v as u32)?,
                        EnumValueData::I16(v) => output.write(v as i32)?,
                        EnumValueData::U32(v) => output.write(v)?,
                        EnumValueData::I32(v) => output.write(v)?,
                    },
                    EnumValue::Name(n) => {
                        let o = find_absolute_nameless_enum_value(e, n);
                        if let Some(o) = o {
                            match o {
                                EnumValueData::U8(v) => output.write(v as u32)?,
                                EnumValueData::I8(v) => output.write(v as i32)?,
                                EnumValueData::U16(v) => output.write(v as u32)?,
                                EnumValueData::I16(v) => output.write(v as i32)?,
                                EnumValueData::U32(v) => output.write(v)?,
                                EnumValueData::I32(v) => output.write(v)?,
                            }
                        } else {
                            return Err(std::io::Error::new(
                                std::io::ErrorKind::InvalidData,
                                format!("No matches found for enum value {:?} at offset: {}", data.get_string(n), output.writer().stream_position()?),
                            ));
                        }
                    }
                }
            }
        }
        let mut file = BufWriter::new(File::create(path)?);
        let data = output.into_writer().into_inner()?.into_inner();
        file.write_all(&data)?;
        Ok(())
    }

    fn load_classes_from_xml_file(path: impl AsRef<Path>, mode: KIClassMode) -> ClassData {
        let hash_string = move |s: &[u8]| {
            match mode {
                KIClassMode::Wizard => hash_string(s),
                KIClassMode::Pirate => light_hash_string(s),
            }
        };
        let file = File::open(path).unwrap();
        let file = BufReader::new(file);
        let parser = EventReader::new_with_config(
            file,
            xml::ParserConfig::new().override_encoding(Some(xml::Encoding::Latin1)),
        );
        let mut output = ClassData {
            strings: StringTable::new(),
            classes: AHashMap::new(),
            enums: AHashMap::new(),
            bitflags: Vec::new(),
            mode
        };

        let mut class = KIClassLayout {
            name: StringPtr::new(None, 0),
            hash: 0,
            base: None,
            server_only: false,
            fields: vec![],
        };
        let mut xml_rdr = parser.into_iter();
        'xml: while let Some(e) = xml_rdr.next() {
            match e {
                Ok(XmlEvent::StartElement {
                    name, attributes, ..
                }) => {
                    if name.local_name == "Option" {
                    } else if name.local_name == "Class" {
                        for attr in attributes {
                            match attr.name.local_name.as_ref() {
                                "Name" => {
                                    let name = attr.value.to_string();
                                    class.hash = hash_string(name.as_bytes());
                                    if let Some(i) = output.strings.get_index_of(&name) {
                                        class.name = StringPtr::new(Some(i as u32), output.strings.generation());
                                    } else {
                                        class.name = output.strings.insert(name);
                                    }
                                }
                                "Base" => {
                                    let base = attr.value.to_string();
                                    class.base = NonZeroU32::new(hash_string(base.as_bytes()));
                                }
                                _ => {}
                            }
                        }
                    } else if name.local_name == "Property" {
                        let mut field = KIClassFieldLayout {
                            name: StringPtr::new(None, 0),
                            flags: FieldFlags::empty(),
                            hash: 0,
                            ty: KIClassType::Unknown,
                            default_value: None,
                            memory_storage: MemoryStorage::Value,
                            container: Container::Single,
                            server_only: false,
                        };
                        for attr in attributes {
                            match attr.name.local_name.as_ref() {
                                "Name" => {
                                    field.name = output.get_or_add_string(attr.value);
                                }
                                "Type" => {
                                    let ty = attr.value.to_string();
                                    let Some(name) = output
                                        .strings
                                        .get(field.name)
                                    else {
                                        panic!(
                                            "Somehow the type's name entry is missing for type {}.",
                                            ty
                                        );
                                    };
                                    field.hash = hash_string(ty.as_bytes()) + light_hash_string(name.as_bytes());
                                    let ty = if attr.value.starts_with("class SharedPointer") {
                                        field.memory_storage = MemoryStorage::SharedPointer;
                                        attr.value
                                            .replace("class SharedPointer<", "")
                                            .trim_end_matches('>')
                                            .to_string()
                                    } else if attr.value.ends_with('*') {
                                        field.memory_storage = MemoryStorage::RawPointer;
                                        attr.value.trim_end_matches('*').to_string()
                                    } else {
                                        attr.value
                                    };
                                    if let Ok(t) = ki_class_type_from_str(&ty, mode) {
                                        field.ty = t;
                                    } else {
                                        warn!("Have not yet handled: {:?}", ty);
                                    }
                                }
                                "Flags" => {
                                    field.flags = FieldFlags::from_bits_retain(
                                        u32::from_str(&attr.value).expect("Not a valid u32 value."),
                                    );
                                }
                                "Container" => {
                                    let container = attr.value.to_string();
                                    field.container = Container::from_str(&container)
                                        .unwrap_or_else(|_| {
                                            panic!("{container} is not a handled container type")
                                        })
                                }
                                "Default" => {
                                    let default = attr.value.to_string();
                                    let default = match field.ty {
                                        KIClassType::Unknown => None,
                                        KIClassType::U8 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::U8(0)
                                        } else {
                                            DefaultKIFieldValue::U8(
                                                u8::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::I8 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::I8(0)
                                        } else {
                                            let val = default.as_bytes()[0];
                                            DefaultKIFieldValue::I8(val as i8)
                                        }),
                                        KIClassType::U16 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::U16(0)
                                        } else {
                                            DefaultKIFieldValue::U16(
                                                u16::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::I16 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::I16(0)
                                        } else {
                                            DefaultKIFieldValue::I16(
                                                i16::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::U32 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::U32(0)
                                        } else {
                                            DefaultKIFieldValue::U32(
                                                u32::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::I32 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::I32(0)
                                        } else {
                                            DefaultKIFieldValue::I32(
                                                i32::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::U64 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::U64(0)
                                        } else {
                                            DefaultKIFieldValue::U64(
                                                u64::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::I64 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::I64(0)
                                        } else {
                                            DefaultKIFieldValue::I64(
                                                i64::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::F32 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::F32(0.0)
                                        } else {
                                            DefaultKIFieldValue::F32(
                                                f32::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::F64 => Some(if default.is_empty() {
                                            DefaultKIFieldValue::F64(0.0)
                                        } else {
                                            DefaultKIFieldValue::F64(
                                                f64::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::Bool => Some(if default.is_empty() {
                                            DefaultKIFieldValue::Bool(false)
                                        } else {
                                            DefaultKIFieldValue::Bool(default != "0")
                                        }),
                                        KIClassType::String => Some(if default.is_empty() {
                                            DefaultKIFieldValue::String(String::new())
                                        } else {
                                            DefaultKIFieldValue::String(default)
                                        }),
                                        KIClassType::WString => Some(if default.is_empty() {
                                            DefaultKIFieldValue::WString(String::new())
                                        } else {
                                            DefaultKIFieldValue::WString(default)
                                        }),
                                        KIClassType::BitInt(_) => Some(if default.is_empty() {
                                            DefaultKIFieldValue::BitInt(0)
                                        } else {
                                            DefaultKIFieldValue::BitInt(
                                                u64::from_str(&default).unwrap_or_default(),
                                            )
                                        }),
                                        KIClassType::Gid => Some(if default.is_empty() {
                                            DefaultKIFieldValue::Gid(0)
                                        } else {
                                            DefaultKIFieldValue::Gid(
                                                u64::from_str(default.split_at(4).1)
                                                    .unwrap_or_default(),
                                            )
                                        }),
                                        //TODO: Past this point is lazy for now
                                        KIClassType::Vector3D => {
                                            Some(DefaultKIFieldValue::Vector3D(Vector3D::default()))
                                        }
                                        KIClassType::PointInt => {
                                            Some(DefaultKIFieldValue::PointInt(PointInt::default()))
                                        }
                                        KIClassType::PointFloat => Some(
                                            DefaultKIFieldValue::PointFloat(PointFloat::default()),
                                        ),
                                        KIClassType::PointUInt => Some(
                                            DefaultKIFieldValue::PointUInt(PointUInt::default()),
                                        ),
                                        KIClassType::RectFloat => Some(
                                            DefaultKIFieldValue::RectFloat(RectFloat::default()),
                                        ),
                                        KIClassType::RectInt => {
                                            Some(DefaultKIFieldValue::RectInt(RectInt::default()))
                                        }
                                        KIClassType::RectUInt => Some(
                                            DefaultKIFieldValue::RectUInt(RectUInt::default()),
                                        ),
                                        KIClassType::Matrix3x3 => Some(
                                            DefaultKIFieldValue::Matrix3x3(Matrix3x3::default()),
                                        ),
                                        KIClassType::SizeInt => {
                                            Some(DefaultKIFieldValue::SizeInt(SizeInt::default()))
                                        }
                                        KIClassType::SizeFloat => Some(
                                            DefaultKIFieldValue::SizeFloat(SizeFloat::default()),
                                        ),
                                        KIClassType::SizeUInt => {
                                            Some(DefaultKIFieldValue::SizeUInt(SizeUInt::default()))
                                        }
                                        KIClassType::UUniqueID => Some(
                                            DefaultKIFieldValue::UUniqueID(UUniqueID::default()),
                                        ),
                                        KIClassType::Color => {
                                            Some(DefaultKIFieldValue::Color(Color::default()))
                                        }
                                        KIClassType::Enum(_) => {
                                            Some(DefaultKIFieldValue::Enum(EnumValueData::I32(0)))
                                        }
                                        KIClassType::BitFlags(_) => {
                                            Some(DefaultKIFieldValue::Enum(EnumValueData::I32(0)))
                                        }
                                        KIClassType::PirateNameIndices => {
                                            //TODO
                                            None
                                        }
                                        KIClassType::Class(_) | KIClassType::SerializeMap(_, _) | KIClassType::SerializePair(_, _) => None,
                                    };
                                    field.default_value = default;
                                }
                                "PropertyId" => {
                                    //field.property_id = u8::from_str(&attr.value).unwrap();
                                }
                                "Pointer"
                                    if field.memory_storage == MemoryStorage::Value => {
                                        field.memory_storage = MemoryStorage::RawPointer;
                                    }
                                _ => {}
                            }
                        }
                        if !field.name.exists() {
                            error!("found a field with no name? the class name is: {:?}", output.get_string(class.name).unwrap_or("Unknown Class"));
                        }
                        let flags = field.flags();
                        if let Some(default) = &mut field.default_value {
                            if flags.contains(FieldFlags::BITS) {
                                match default {
                                    DefaultKIFieldValue::Enum(_) => {}
                                    DefaultKIFieldValue::U32(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::U32(*v));
                                    }
                                    DefaultKIFieldValue::I32(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::I32(*v));
                                    }
                                    DefaultKIFieldValue::U16(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::U16(*v));
                                    }
                                    DefaultKIFieldValue::I16(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::I16(*v));
                                    }
                                    DefaultKIFieldValue::U8(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::U8(*v));
                                    }
                                    DefaultKIFieldValue::I8(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::I8(*v));
                                    }
                                    // DefaultKIFieldValue::BitInt(v) => {
                                    //     *default = DefaultKIFieldValue::Enum(EnumValueData::U32(*v as u32));
                                    // }
                                    _ => {
                                        warn!("Found a default value that is not an integer for a bitflag field {:?} on class {:?}, instead it is: {:?}. Ignoring the default value. Please report this to the developer.", output.get_string(field.name), output.get_string(class.name), default);
                                        field.default_value = None;
                                    }
                                }
                            } else if flags.contains(FieldFlags::ENUM) {
                                match default {
                                    DefaultKIFieldValue::Enum(_) => {}
                                    DefaultKIFieldValue::U32(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::U32(*v));
                                    }
                                    DefaultKIFieldValue::I32(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::I32(*v));
                                    }
                                    DefaultKIFieldValue::U16(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::U16(*v));
                                    }
                                    DefaultKIFieldValue::I16(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::I16(*v));
                                    }
                                    DefaultKIFieldValue::U8(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::U8(*v));
                                    }
                                    DefaultKIFieldValue::I8(v) => {
                                        *default = DefaultKIFieldValue::Enum(EnumValueData::I8(*v));
                                    }
                                    // DefaultKIFieldValue::BitInt(v) => {
                                    //     *default = DefaultKIFieldValue::Enum(EnumValueData::U32(*v as u32));
                                    // }
                                    _ => {
                                        warn!("Found a default value that is not an integer for an enum field {:?} on class {:?}, instead it is: {:?}. Ignoring the default value. Please report this to the developer.", output.get_string(field.name), output.get_string(class.name), default);
                                        field.default_value = None;
                                    }
                                }
                            }
                        }
                        class.fields.push(field);
                    } else if name.local_name == "Enum"
                        && let Some(KIClassFieldLayout {
                            name: _, flags, ty, ..
                        }) = class.fields.last_mut()
                        {
                            if !matches!(ty, KIClassType::Enum(_))
                                && !flags.contains(FieldFlags::BITS)
                            {
                                continue;
                            }
                            if flags.contains(FieldFlags::BITS) {
                                let mut e = KIBitFlagsLayout {
                                    default_value: EnumValueData::U32(0),
                                    value_list: vec![],
                                };
                                for at in xml_rdr.by_ref() {
                                    match at {
                                        Ok(event) => match event {
                                            XmlEvent::StartElement {
                                                name, attributes, ..
                                            }
                                                if name.local_name == "Option" => {
                                                    let mut option = (
                                                        StringPtr::new(None, 0),
                                                        EnumValue::Value(EnumValueData::I32(0)),
                                                    );
                                                    for attr in attributes {
                                                        if attr.name.local_name == "Name" {
                                                            option.0 = output
                                                                .get_or_add_string(attr.value);
                                                        } else if attr.name.local_name == "Value" {
                                                            match ty {
                                                                KIClassType::U32 => {
                                                                    if let Ok(value) =
                                                                        u32::from_str(&attr.value)
                                                                    {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::U32(
                                                                                value,
                                                                            ),
                                                                        );
                                                                    } else if &attr.value == "-2147483648" {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::U32(
                                                                                u32::MAX,
                                                                            ),
                                                                        );
                                                                    } else {
                                                                        let ptr = output
                                                                            .get_or_add_string(
                                                                                attr.value,
                                                                            );
                                                                        option.1 =
                                                                            EnumValue::Name(ptr);
                                                                    }
                                                                }
                                                                KIClassType::I32 => {
                                                                    if let Ok(value) =
                                                                        i32::from_str(&attr.value)
                                                                    {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::I32(
                                                                                value,
                                                                            ),
                                                                        );
                                                                    } else {
                                                                        let ptr = output
                                                                            .get_or_add_string(
                                                                                attr.value,
                                                                            );
                                                                        option.1 =
                                                                            EnumValue::Name(ptr);
                                                                    }
                                                                }
                                                                KIClassType::U16 => {
                                                                    if let Ok(value) =
                                                                        u16::from_str(&attr.value)
                                                                    {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::U16(
                                                                                value,
                                                                            ),
                                                                        );
                                                                    } else {
                                                                        let ptr = output
                                                                            .get_or_add_string(
                                                                                attr.value,
                                                                            );
                                                                        option.1 =
                                                                            EnumValue::Name(ptr);
                                                                    }
                                                                }
                                                                KIClassType::I16 => {
                                                                    if let Ok(value) =
                                                                        i16::from_str(&attr.value)
                                                                    {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::I16(
                                                                                value,
                                                                            ),
                                                                        );
                                                                    } else {
                                                                        let ptr = output
                                                                            .get_or_add_string(
                                                                                attr.value,
                                                                            );
                                                                        option.1 =
                                                                            EnumValue::Name(ptr);
                                                                    }
                                                                }
                                                                KIClassType::U8 => {
                                                                    if let Ok(value) =
                                                                        u8::from_str(&attr.value)
                                                                    {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::U8(
                                                                                value,
                                                                            ),
                                                                        );
                                                                    } else {
                                                                        let ptr = output
                                                                            .get_or_add_string(
                                                                                attr.value,
                                                                            );
                                                                        option.1 =
                                                                            EnumValue::Name(ptr);
                                                                    }
                                                                }
                                                                KIClassType::I8 => {
                                                                    if let Ok(value) =
                                                                        i8::from_str(&attr.value)
                                                                    {
                                                                        option.1 = EnumValue::Value(
                                                                            EnumValueData::I8(
                                                                                value,
                                                                            ),
                                                                        );
                                                                    } else {
                                                                        let ptr = output
                                                                            .get_or_add_string(
                                                                                attr.value,
                                                                            );
                                                                        option.1 =
                                                                            EnumValue::Name(ptr);
                                                                    }
                                                                }
                                                                _ => panic!("Invalid enum type? on {} on class {}", attr.value, output.get_string(class.name).unwrap_or("Unknown Class")),
                                                            }
                                                        }
                                                    }
                                                    e.value_list.push(option);
                                                }
                                            XmlEvent::EndElement { name }
                                                if name.local_name == "Enum" => {
                                                    break;
                                                }
                                            _ => {}
                                        },
                                        Err(err) => {
                                            error!("{:?}", err);
                                        }
                                    }
                                }

                                *ty = KIClassType::BitFlags(output.bitflags.len() as u32);
                                output.bitflags.push(e.into());
                            } else {
                                let mut e = KIEnumLayout {
                                    hash: 0,
                                    name: StringPtr::new(None, 0),
                                    default_value: EnumValueData::U32(0),
                                    value_list: vec![],
                                };
                                for attr in attributes {
                                    if attr.name.local_name == "Name" {
                                        let hash = hash_string(attr.value.as_bytes());
                                        if output.enums.contains_key(&hash) {
                                            continue 'xml;
                                        }
                                        let name = output.get_or_add_string(attr.value);
                                        e.hash = hash;
                                        e.name = name;
                                    };
                                }
                                for at in xml_rdr.by_ref() {
                                    match at {
                                        Ok(event) => {
                                            match event {
                                                XmlEvent::StartElement {
                                                    name, attributes, ..
                                                }
                                                    if name.local_name == "Option" => {
                                                        let mut option = (
                                                            StringPtr::new(None, 0),
                                                            EnumValue::Value(EnumValueData::I32(0)),
                                                        );
                                                        for attr in attributes {
                                                            if attr.name.local_name == "Name" {
                                                                option.0 = output
                                                                    .get_or_add_string(attr.value);
                                                            } else if attr.name.local_name
                                                                == "Value"
                                                            {
                                                                match ty {
                                                                    KIClassType::U8 => {
                                                                        if let Ok(value) =
                                                                            u8::from_str(
                                                                                &attr.value,
                                                                            )
                                                                        {
                                                                            option.1 = EnumValue::Value(EnumValueData::U8(value));
                                                                        } else {
                                                                            let ptr = output
                                                                                .get_or_add_string(
                                                                                    attr.value,
                                                                                );
                                                                            option.1 =
                                                                                EnumValue::Name(
                                                                                    ptr,
                                                                                );
                                                                        }
                                                                    }
                                                                    KIClassType::I8 => {
                                                                        if let Ok(value) =
                                                                            i8::from_str(
                                                                                &attr.value,
                                                                            )
                                                                        {
                                                                            option.1 = EnumValue::Value(EnumValueData::I8(value));
                                                                        } else {
                                                                            let ptr = output
                                                                                .get_or_add_string(
                                                                                    attr.value,
                                                                                );
                                                                            option.1 =
                                                                                EnumValue::Name(
                                                                                    ptr,
                                                                                );
                                                                        }
                                                                    }
                                                                    KIClassType::U16 => {
                                                                        if let Ok(value) =
                                                                            u16::from_str(
                                                                                &attr.value,
                                                                            )
                                                                        {
                                                                            option.1 = EnumValue::Value(EnumValueData::U16(value));
                                                                        } else {
                                                                            let ptr = output
                                                                                .get_or_add_string(
                                                                                    attr.value,
                                                                                );
                                                                            option.1 =
                                                                                EnumValue::Name(
                                                                                    ptr,
                                                                                );
                                                                        }
                                                                    }
                                                                    KIClassType::I16 => {
                                                                        if let Ok(value) =
                                                                            i16::from_str(
                                                                                &attr.value,
                                                                            )
                                                                        {
                                                                            option.1 = EnumValue::Value(EnumValueData::I16(value));
                                                                        } else {
                                                                            let ptr = output
                                                                                .get_or_add_string(
                                                                                    attr.value,
                                                                                );
                                                                            option.1 =
                                                                                EnumValue::Name(
                                                                                    ptr,
                                                                                );
                                                                        }
                                                                    }
                                                                    KIClassType::U32 => {
                                                                        if let Ok(value) =
                                                                            u32::from_str(
                                                                                &attr.value,
                                                                            )
                                                                        {
                                                                            option.1 = EnumValue::Value(EnumValueData::U32(value));
                                                                        } else {
                                                                            let ptr = output
                                                                                .get_or_add_string(
                                                                                    attr.value,
                                                                                );
                                                                            option.1 =
                                                                                EnumValue::Name(
                                                                                    ptr,
                                                                                );
                                                                        }
                                                                    }
                                                                    KIClassType::Enum(_)
                                                                    | KIClassType::I32 => {
                                                                        if let Ok(value) =
                                                                            i32::from_str(
                                                                                &attr.value,
                                                                            )
                                                                        {
                                                                            option.1 = EnumValue::Value(EnumValueData::I32(value));
                                                                        } else {
                                                                            let ptr = output
                                                                                .get_or_add_string(
                                                                                    attr.value,
                                                                                );
                                                                            option.1 =
                                                                                EnumValue::Name(
                                                                                    ptr,
                                                                                );
                                                                        }
                                                                    }
                                                                    _ => {
                                                                        panic!("Invalid enum type?")
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        e.value_list.push(option);
                                                    }
                                                XmlEvent::EndElement { name }
                                                    if name.local_name == "Enum" => {
                                                        break;
                                                    }
                                                _ => {}
                                            }
                                        }
                                        Err(err) => {
                                            error!("{:?}", err);
                                        }
                                    }
                                }
                                output.enums.insert(e.hash, e.into());
                            }
                        }
                }
                Ok(XmlEvent::EndElement { name }) => {
                    if name.local_name == "Enum" {
                        // enums.insert(hash_string(&_enum.name), _enum);
                        // //log!("Added enum to map");
                        // _enum = Enum {
                        //     name: "".to_string(),
                        //     values: Default::default(),
                        // };
                    } else if name.local_name == "Class" {
                        //log!("Added class: {} to map", class.name.clone());
                        if !class.name.exists() {
                            warn!("found a class with no name? how odd.");
                        }
                        output.classes.insert(class.hash, class.into());
                        class = KIClassLayout {
                            name: StringPtr::new(None, 0), //means null
                            base: None,                 //means null
                            hash: 0,
                            server_only: false,
                            fields: vec![],
                        };
                    }
                }
                Err(e) => {
                    error!("Error: {}", e);
                    break;
                }
                _ => {}
            }
        }
        output
    }

    //TODO: merge nameless enums
    pub fn merge(
        client: impl AsRef<Path>,
        server: impl AsRef<Path>,
        output: impl AsRef<Path>
    ) -> std::io::Result<()> {
        let mut client = Self::load_from_file(client)?;
        let mut server = Self::load_from_file(server)?;
        if client.mode != server.mode {
            return Err(std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                format!("Client and server class data have different modes: Client mode: {:?}, Server mode: {:?}", client.mode, server.mode),
            ));
        }
        let mut merged = ClassData {
            strings: StringTable::new(),
            classes: AHashMap::new(),
            enums: AHashMap::new(),
            bitflags: Vec::new(),
            // Doesn't matter which one we take since they have to be the same
            mode: client.mode,
        };
        for mut e in client.bitflags.drain(..).collect::<Vec<_>>().drain(..) {
            let bitflags = Arc::get_mut(&mut e).unwrap();
            for v in bitflags.value_list.iter_mut() {
                if let Some(name) = client.get_string(v.0) {
                    let nid = merged.find_string(name);
                    if nid.exists() {
                        v.0 = nid;
                    } else {
                        v.0 = merged.get_or_add_string(name.to_string());
                    }
                }
            }
            merged.bitflags.push(e);
        }
        for (cid, mut class) in client.classes.drain().collect::<Vec<_>>().drain(..) {
            let client_class = Arc::get_mut(&mut class).unwrap();
            if let Some(v) = server.classes.get(&cid) {
                let server_class = v.clone();
                if client_class.fields.len() < server_class.fields.len() {
                    'main: for f1 in server_class.fields.iter() {
                        for f2 in client_class.fields.iter() {
                            if f1.hash() == f2.hash() {
                                continue 'main;
                            }
                        }
                        let name = f1.name;
                        let v = server.get_string(name).unwrap();
                        let client_name = client.find_string(v);
                        let name = if client_name == StringPtr::new(None, client.strings.generation()) {
                            client.strings.insert(v.to_string())
                        } else {
                            client_name
                        };
                        client_class.fields.push(KIClassFieldLayout {
                            name,
                            server_only: true,
                            ..f1.clone()
                        });
                    }
                }
            }
            let name_ptr = client_class.name;
            if let Some(name) = client.strings.get(name_ptr) {
                if let Some(ptr) = merged.strings.get_ptr_of(name) {
                    client_class.name = ptr;
                } else {
                    client_class.name = merged.strings.insert(name.to_string());
                }
            }
            for i in 0..client_class.fields.len() {
                let field = &mut client_class.fields[i];
                if let Some(name) = client.strings.get(field.name) {
                    if let Some(ptr) = merged.strings.get_ptr_of(name) {
                        field.name = ptr;
                    } else {
                        field.name = merged.strings.insert(name.to_string());
                    }
                }
            }
            server.classes.remove(&cid);
            merged.classes.insert(cid, class);
        }

        for (cid, mut class) in server.classes.drain() {
            let mut_class = Arc::get_mut(&mut class).unwrap();
            if let Some(v) = merged.classes.get(&cid)
                && mut_class.fields.len() > v.fields.len() {
                    warn!("Class field mismatch: {:?}", merged.get_string(v.name()));
                    'main: for f1 in mut_class.fields.iter_mut() {
                        for f2 in v.fields.iter() {
                            if f1.hash() == f2.hash() {
                                continue 'main;
                            }
                        }
                        f1.server_only = true;
                    }
                }
            let name_ptr = mut_class.name;
            if let Some(name) = server.strings.get(name_ptr) {
                if let Some(ptr) = merged.strings.get_ptr_of(name) {
                    mut_class.name = ptr;
                } else {
                    mut_class.name = merged.strings.insert(name.to_string());
                }
            }
            for i in 0..mut_class.fields.len() {
                let field = &mut mut_class.fields[i];
                if let KIClassType::BitFlags(i) = field.ty
                    && let Some(bitflag) = server.bitflags.get_mut(i as usize) {
                        let bf = Arc::get_mut(bitflag).unwrap();
                        for v in bf.value_list.iter_mut() {
                            if let Some(name) = client.get_string(v.0) {
                                let nid = merged.find_string(name);
                                if nid.exists() {
                                    v.0 = nid;
                                } else {
                                    v.0 = merged.get_or_add_string(name.to_string());
                                }
                            }
                        }
                        field.ty = KIClassType::BitFlags(merged.bitflags.len() as u32);
                        merged.bitflags.push(bitflag.clone());
                    }
                if let Some(name) = server.strings.get(field.name) {
                    if let Some(ptr) = merged.strings.get_ptr_of(name) {
                        field.name = ptr;
                    } else {
                        field.name = merged.strings.insert(name.to_string());
                    }
                }
            }
            merged.classes.insert(
                cid,
                KIClassLayout {
                    server_only: true,
                    name: mut_class.name,
                    base: mut_class.base,
                    hash: mut_class.hash,
                    fields: mut_class.fields.clone(),
                }
                .into(),
            );
        }
        for (eid, mut e) in client.enums.drain().collect::<Vec<_>>().drain(..) {
            let enum_class = Arc::get_mut(&mut e).unwrap();
            if let Some(name) = client.get_string(enum_class.name) {
                let nid = merged.find_string(name);
                if nid.exists() {
                    enum_class.name = nid;
                }
            }
            for v in enum_class.value_list.iter_mut() {
                if let Some(name) = client.get_string(v.0) {
                    let nid = merged.find_string(name);
                    if nid.exists() {
                        v.0 = nid;
                    } else {
                        v.0 = merged.get_or_add_string(name.to_string());
                    }
                }
            }
            merged.enums.insert(eid, e);
        }
        for (eid, mut e) in server.enums.drain().collect::<Vec<_>>().drain(..) {
            if merged.enums.contains_key(&eid) {
                continue;
            }
            let enum_class = Arc::get_mut(&mut e).unwrap();
            if let Some(name) = server.get_string(enum_class.name) {
                let nid = merged.find_string(name);
                if nid.exists() {
                    enum_class.name = nid;
                }
            }
            for v in enum_class.value_list.iter_mut() {
                if let Some(name) = server.get_string(v.0) {
                    let nid = merged.find_string(name);
                    if nid.exists() {
                        v.0 = nid;
                    } else {
                        v.0 = merged.get_or_add_string(name.to_string());
                    }
                }
            }
            merged.enums.insert(eid, e);
        }
        //TODO: merge nameless enums
        Self::save_to_file(merged, output)
    }
}

fn find_absolute_enum_value(e: &KIEnumLayout, name: StringPtr) -> Option<EnumValueData> {
    for ov in e.value_list.iter() {
        if ov.0 == name {
            match ov.1 {
                EnumValue::Value(v) => return Some(v),
                EnumValue::Name(v) => return find_absolute_enum_value(e, v),
            }
        }
    }
    None
}

fn find_absolute_nameless_enum_value(
    e: &KIBitFlagsLayout,
    name: StringPtr,
) -> Option<EnumValueData> {
    for ov in e.value_list.iter() {
        if ov.0 == name {
            match ov.1 {
                EnumValue::Value(v) => return Some(v),
                EnumValue::Name(v) => return find_absolute_nameless_enum_value(e, v),
            }
        }
    }
    None
}
