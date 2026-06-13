use std::str::FromStr;

use crate::{hashing::{hash_string, light_hash_string}, python_wrappers::KIClassMode};

use super::errors::ParseClassTypeError;

#[derive(Debug, Clone, Copy)]
pub enum KIClassType {
    Unknown,
    U8,
    I8,
    U16,
    I16,
    U32,
    I32,
    U64,
    I64,
    F32,
    F64,
    Bool,
    String,
    WString,
    Gid,
    BitInt(u8),
    Vector3D,
    PointFloat,
    PointInt,
    PointUInt,
    RectFloat,
    RectInt,
    RectUInt,
    Matrix3x3,
    SizeInt,
    SizeFloat,
    SizeUInt,
    UUniqueID,
    Color,
    Enum(u32),
    BitFlags(u32),
    Class(u32),
    SerializeMap(u32, u32), //key type, value type
    SerializePair(u32, u32), //first type, second type
    PirateNameIndices
}

pub fn ki_class_type_from_str(s: &str, mode: KIClassMode) -> Result<KIClassType, ParseClassTypeError> {
    let hash_string = move |s: &[u8]| {
        match mode {
            KIClassMode::Wizard => hash_string(s),
            KIClassMode::Pirate => light_hash_string(s),
        }
    };
    Ok(match s {
            "bool" => KIClassType::Bool,
            "char" | "s8" => KIClassType::I8,
            "unsigned char" | "u8" => KIClassType::U8,
            "short" | "s16" => KIClassType::I16,
            "unsigned short" | "u16" => KIClassType::U16,
            "int" | "s32" | "sl32" => KIClassType::I32,
            "unsigned int" | "u32" | "ul32" => KIClassType::U32,
            "unsigned __int64" | "u64" => KIClassType::U64,
            "long" => KIClassType::I32,
            "unsigned long" => KIClassType::U32,
            "__int64" | "s64" => KIClassType::I64,
            "gid" => KIClassType::Gid,
            "float" | "f32" => KIClassType::F32,
            "double" | "f64" => KIClassType::F64,
            "std::string" => KIClassType::String,
            "std::wstring" => KIClassType::WString,
            "wchar_t" => KIClassType::U16,
            "bui1" => KIClassType::BitInt(1),
            "bui2" => KIClassType::BitInt(2),
            "bui3" => KIClassType::BitInt(3),
            "bui4" => KIClassType::BitInt(4),
            "bui5" => KIClassType::BitInt(5),
            "bui6" => KIClassType::BitInt(6),
            "bui7" => KIClassType::BitInt(7),
            "bi1" => KIClassType::BitInt(1),
            "bi2" => KIClassType::BitInt(2),
            "bi3" => KIClassType::BitInt(3),
            "bi4" => KIClassType::BitInt(4),
            "bi5" => KIClassType::BitInt(5),
            "bi6" => KIClassType::BitInt(6),
            "bi7" => KIClassType::BitInt(7),
            "s24" => KIClassType::BitInt(24),
            "u24" => KIClassType::BitInt(24),
            //TODO: add bit floats
            "buf4" => KIClassType::Unknown,  //TODO
            "buf8" => KIClassType::Unknown,  //TODO
            "buf16" => KIClassType::Unknown, //TODO
            "bf4" => KIClassType::Unknown,   //TODO
            "bf8" => KIClassType::Unknown,   //TODO
            "bf16" => KIClassType::Unknown,  //TODO
            "class Vector3D" => KIClassType::Vector3D,
            "class Point<int>" => KIClassType::PointInt,
            "class Point<float>" => KIClassType::PointFloat,
            "class Point<unsigned int>" => KIClassType::PointUInt,
            "class Rect<float>" => KIClassType::RectFloat,
            "class Rect<int>" => KIClassType::RectInt,
            "class Rect<unsigned int>" => KIClassType::RectUInt,
            "class Size<float>" => KIClassType::SizeFloat,
            "class Size<int>" => KIClassType::SizeInt,
            "class Size<unsigned int>" => KIClassType::SizeUInt,
            "class UUniqueID" => KIClassType::UUniqueID,
            "class Color" => KIClassType::Color,
            "class Matrix3x3" => KIClassType::Matrix3x3,
            "class Quaternion" => KIClassType::Unknown, //TODO
            "class Euler" => KIClassType::Unknown,      //TODO
            "PirateNameIndices" => KIClassType::PirateNameIndices,
            t if t.starts_with("SerializePair") => {
                let types: Vec<&str> = t["SerializePair<".len()..t.len() - 1].split(',').map(|s| s.trim()).collect();
                if types.len() != 2 {
                    return Err(ParseClassTypeError);
                }
                KIClassType::SerializePair(hash_string(types[0].as_bytes()), hash_string(types[1].as_bytes()))
            },
            t if t.starts_with("SerializeMap") => {
                let types: Vec<&str> = t["SerializeMap<".len()..t.len() - 1].split(',').map(|s| s.trim()).collect();
                if types.len() != 2 {
                    return Err(ParseClassTypeError);
                }
                KIClassType::SerializeMap(hash_string(types[0].as_bytes()), hash_string(types[1].as_bytes()))
            },
            t if t.starts_with("class ") || t.starts_with("struct ") => {
                KIClassType::Class(hash_string(t.as_bytes()))
            }
            t if t.starts_with("enum ") => KIClassType::Enum(hash_string(t.as_bytes())),
            _ => {
                return Err(ParseClassTypeError);
            }
        })
}

impl KIClassType {
    pub fn tag(&self) -> u8 {
        match self {
            KIClassType::Unknown => 0,
            KIClassType::U8 => 1,
            KIClassType::I8 => 2,
            KIClassType::U16 => 3,
            KIClassType::I16 => 4,
            KIClassType::U32 => 5,
            KIClassType::I32 => 6,
            KIClassType::U64 => 7,
            KIClassType::I64 => 8,
            KIClassType::F32 => 9,
            KIClassType::F64 => 10,
            KIClassType::Bool => 11,
            KIClassType::String => 12,
            KIClassType::WString => 13,
            KIClassType::Gid => 14,
            KIClassType::BitInt(_) => 15, //TODO: differentiate bit ints
            KIClassType::Vector3D => 16,
            KIClassType::PointFloat => 17,
            KIClassType::PointInt => 18,
            KIClassType::PointUInt => 19,
            KIClassType::Matrix3x3 => 20,
            KIClassType::SizeInt => 21,
            KIClassType::SizeFloat => 22,
            KIClassType::SizeUInt => 23,
            KIClassType::UUniqueID => 24,
            KIClassType::Color => 25,
            KIClassType::Enum(_) => 26,
            KIClassType::BitFlags(_) => 27,
            KIClassType::Class(_) => 28,
            KIClassType::SerializeMap(_, _) => 29,
            KIClassType::SerializePair(_, _) => 30,
            KIClassType::PirateNameIndices => 31,
            KIClassType::RectFloat => 32,
            KIClassType::RectInt => 33,
            KIClassType::RectUInt => 34,
        }
    }
}
