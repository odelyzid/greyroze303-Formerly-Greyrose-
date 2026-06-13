use std::{
    cell::RefCell,
    io::{Cursor, Read},
    path::Path,
    sync::Arc,
};

use bitflags::bitflags;
use bitstream_io::{BitRead, BitReader, LittleEndian, Primitive};
use log::warn;
use smol_str::SmolStr;
use uuid::Uuid;

use crate::classes::{
    ClassData,
    builtins::{
        Color, Matrix3x3, PointFloat, PointInt, PointUInt, RectFloat, RectInt, RectUInt,
        SizeFloat, SizeInt, SizeUInt, UUniqueID, Vector3D,
    },
    container::Container,
    default::DefaultKIFieldValue,
    layout::{
        EnumValue, EnumValueData, KIBitFlagsLayout, KIClassFieldLayout, KIClassLayout, KIEnumLayout,
    },
    ty::KIClassType,
};

bitflags! {
    #[derive(Debug, Clone, Copy, PartialEq, Eq)]
    pub struct SerializationOptions: u32 {
        const ENCODE_OPTIONS = 0x1;
        const COMPRESSED_INDEX = 0x2;
        const VERSIONED_ENUM = 0x4;
        const ZLIB_COMPRESS = 0x8;
        const IGNORE_DELTA_SAVE = 0x10;
        const DEFAULT = 0;
        const COMPACT = Self::COMPRESSED_INDEX.bits() | Self::ZLIB_COMPRESS.bits();
        const ROBUST = Self::ENCODE_OPTIONS.bits() | Self::VERSIONED_ENUM.bits();
        const CURRENT = Self::COMPACT.bits() | Self::ROBUST.bits();
    }
}

impl Primitive for SerializationOptions {
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
        Self::from_bits_retain(u32::from_be_bytes(bytes))
    }

    fn from_le_bytes(bytes: Self::Bytes) -> Self {
        Self::from_bits_retain(u32::from_le_bytes(bytes))
    }
}

#[derive(Clone)]
pub struct KIBinDeserialize {
    classes: Arc<ClassData>,
}

impl KIBinDeserialize {
    pub fn new(classes: Arc<ClassData>) -> Self {
        Self { classes }
    }
    pub fn classes(&self) -> &Arc<ClassData> {
        &self.classes
    }
    pub fn read_file(&self, path: impl AsRef<Path>) -> std::io::Result<KIDynamicClassResult> {
        thread_local! {
            static BUFFER: RefCell<Vec<u8>> = RefCell::new(Vec::with_capacity(1024));
        };
        BUFFER.with_borrow_mut(|buffer| {
            buffer.clear();
            let mut file = std::fs::File::open(path)?;
            file.read_to_end(buffer)?;
            self.read_data(buffer.as_slice())
        })
    }

    pub fn read_data(&self, data: &[u8]) -> std::io::Result<KIDynamicClassResult> {
        let data = Cursor::new(data);
        let mut rdr = BitReader::endian(data, LittleEndian);
        let signature = rdr.read::<u32>(32)?;
        if signature != 1682852162 {
            return Ok(KIDynamicClassResult::None);
        }
        let options = SerializationOptions::from_bits_retain(rdr.read::<u32>(32)?);
        if options.contains(SerializationOptions::ZLIB_COMPRESS) {
            let compressed = rdr.read::<u8>(8)? > 0;
            if compressed {
                let size = rdr.read::<i32>(32)?;
                let mut decompressor = flate2::Decompress::new(true);
                let mut buf = Vec::with_capacity(1024);
                rdr.reader().unwrap().read_to_end(&mut buf)?;
                let mut output = vec![0u8; size as usize];
                decompressor.decompress(&buf, &mut output, flate2::FlushDecompress::Finish)?;
                return self.read_raw_data(&output, options);
            }
        }
        self.read_class_data(&mut rdr, options)
    }

    pub fn read_raw_file(
        &self,
        path: impl AsRef<Path>,
        options: SerializationOptions,
    ) -> std::io::Result<KIDynamicClassResult> {
        thread_local! {
            static BUFFER: RefCell<Vec<u8>> = RefCell::new(Vec::with_capacity(1024));
        };
        BUFFER.with_borrow_mut(|buffer| {
            buffer.clear();
            let mut file = std::fs::File::open(path)?;
            file.read_to_end(buffer)?;
            self.read_raw_data(buffer.as_slice(), options)
        })
    }

    pub fn read_raw_data(
        &self,
        data: &[u8],
        options: SerializationOptions,
    ) -> std::io::Result<KIDynamicClassResult> {
        let data = Cursor::new(data);
        let mut rdr = BitReader::endian(data, LittleEndian);
        self.read_class_data(&mut rdr, options)
    }

    fn read_class_data<B: AsRef<[u8]>>(
        &self,
        rdr: &mut BitReader<Cursor<B>, LittleEndian>,
        options: SerializationOptions,
    ) -> std::io::Result<KIDynamicClassResult> {
        let mut unaligned = false;
        self.read_class(rdr, options, &mut unaligned)
    }

    fn read_numeric_enum_value<B: AsRef<[u8]>>(
        &self,
        rdr: &mut BitReader<Cursor<B>, LittleEndian>,
        default_value: EnumValueData,
    ) -> std::io::Result<EnumValueData> {
        match default_value {
            EnumValueData::U8(_) => Ok(EnumValueData::U8(rdr.read::<u32>(32)? as u8)),
            EnumValueData::I8(_) => Ok(EnumValueData::I8(rdr.read::<i32>(32)? as i8)),
            EnumValueData::U16(_) => Ok(EnumValueData::U16(rdr.read::<u32>(32)? as u16)),
            EnumValueData::I16(_) => Ok(EnumValueData::I16(rdr.read::<i32>(32)? as i16)),
            EnumValueData::U32(_) => Ok(EnumValueData::U32(rdr.read::<u32>(32)?)),
            EnumValueData::I32(_) => Ok(EnumValueData::I32(rdr.read::<i32>(32)?)),
        }
    }

    fn read_string_enum_value<B: AsRef<[u8]>>(
        &self,
        rdr: &mut BitReader<Cursor<B>, LittleEndian>,
        options: SerializationOptions,
    ) -> std::io::Result<String> {
        let value_name_len = self.read_string_size(rdr, options)? as usize;
        let value_name = rdr.read_to_vec(value_name_len)?;
        String::from_utf8(value_name)
            .map_err(|err| std::io::Error::new(std::io::ErrorKind::InvalidData, err))
    }

    fn read_class<B: AsRef<[u8]>>(
        &self,
        rdr: &mut BitReader<Cursor<B>, LittleEndian>,
        options: SerializationOptions,
        unaligned: &mut bool,
    ) -> std::io::Result<KIDynamicClassResult> {
        let class_id = rdr.read::<u32>(32)?;
        if class_id == 0 {
            //println!("Found a null class. skipping...");
            return Ok(KIDynamicClassResult::None);
        }
        let class_size_in_bits = rdr.read::<u32>(32)?.saturating_sub(32);

        if let Some(class_def) = self.classes.get_class_by_hash(class_id) {
            let mut output = KIDynamicClass::new(class_def.clone(), self.classes());
            let class_end = rdr.position_in_bits()? + class_size_in_bits as u64;
            while rdr.position_in_bits()? < class_end {
                let mut field_size_in_bits = rdr.read::<u32>(32)?.saturating_sub(64);
                if field_size_in_bits % 8 != 0 && field_size_in_bits > 8 && *unaligned {
                    *unaligned = false;
                    field_size_in_bits -= 8;
                }
                let field_id = rdr.read::<u32>(32)?;
                let field = 'h: {
                    for (i, field) in class_def.fields().iter().enumerate() {
                        if field.hash() == field_id {
                            break 'h Some((field, i));
                        }
                    }
                    None
                };
                if let Some((field, field_index)) = field {
                    let container = field.container();
                    let start = rdr.position_in_bits()?;
                    let amount = if container != Container::Single {
                        self.read_container_size(rdr, options)?
                    } else {
                        1
                    };
                    let field_data = &mut output.fields[field_index];
                    for _ in 0..amount {
                        let ty = field.ty();
                        //println!("type: {:?}", ty);
                        match ty {
                            KIClassType::Unknown => {
                                warn!("Unknown type, skipping");
                                rdr.seek_bits(std::io::SeekFrom::Start(start))?;
                                rdr.skip(field_size_in_bits)?;
                            }
                            KIClassType::U8 => {
                                let val = rdr.read::<u8>(8)?;
                                let data = KIDynamicFieldData::U8(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::I8 => {
                                let val = rdr.read::<i8>(8)?;
                                let data = KIDynamicFieldData::I8(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::U16 => {
                                let val = rdr.read::<u16>(16)?;
                                let data = KIDynamicFieldData::U16(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::I16 => {
                                let val = rdr.read::<i16>(16)?;
                                let data = KIDynamicFieldData::I16(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::U32 => {
                                let val = rdr.read::<u32>(32)?;
                                let data = KIDynamicFieldData::U32(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::I32 => {
                                let val = rdr.read::<i32>(32)?;
                                let data = KIDynamicFieldData::I32(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::U64 => {
                                let val = rdr.read::<u64>(64)?;
                                let data = KIDynamicFieldData::U64(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::I64 => {
                                let val = rdr.read::<i64>(64)?;
                                let data = KIDynamicFieldData::I64(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::F32 => {
                                let val = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let data = KIDynamicFieldData::F32(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::F64 => {
                                let val = f64::from_le_bytes(rdr.read::<u64>(64)?.to_le_bytes());
                                let data = KIDynamicFieldData::F64(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::Bool => {
                                let val = rdr.read::<u8>(1)? > 0;
                                let data = KIDynamicFieldData::Bool(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::String => {
                                let size = self.read_string_size(rdr, options)? as usize;
                                let data = rdr.read_to_vec(size)?;
                                let is_str = data.is_ascii();
                                let data = if is_str {
                                    if let Ok(data) = str::from_utf8(data.as_slice()) {
                                        KIDynamicFieldData::String(data.into())
                                    } else {
                                        KIDynamicFieldData::Bytes(data)
                                    }
                                } else {
                                    KIDynamicFieldData::Bytes(data)
                                };
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::WString => {
                                let size = self.read_string_size(rdr, options)? as usize;
                                let mut buf = Vec::with_capacity(size);
                                for _ in 0..size {
                                    let c = rdr.read::<u16>(16)?;
                                    buf.push(c);
                                }
                                let data = String::from_utf16(&buf)
                                    .expect("Failed to convert string from bytes");
                                let data = KIDynamicFieldData::WString(data.into());
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::Gid => {
                                let val = rdr.read::<u64>(64)?;
                                let data = KIDynamicFieldData::U64(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::BitInt(i) => {
                                //TODO test if valid behaviour
                                let val = rdr.read::<u64>(i as u32)?;
                                let data = KIDynamicFieldData::U64(val);
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::Vector3D => {
                                let x = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let y = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let z = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let data = KIDynamicFieldData::Vector3D(Vector3D { x, y, z });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::PointFloat => {
                                let x = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let y = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let data = KIDynamicFieldData::PointFloat(PointFloat { x, y });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::PointInt => {
                                let x = rdr.read::<i32>(32)?;
                                let y = rdr.read::<i32>(32)?;
                                let data = KIDynamicFieldData::PointInt(PointInt { x, y });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::PointUInt => {
                                let x = rdr.read::<u32>(32)?;
                                let y = rdr.read::<u32>(32)?;
                                let data = KIDynamicFieldData::PointUInt(PointUInt { x, y });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::RectFloat => {
                                let left =
                                    f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let top =
                                    f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let right =
                                    f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let bottom =
                                    f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let data = KIDynamicFieldData::RectFloat(RectFloat {
                                    left,
                                    top,
                                    right,
                                    bottom,
                                });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::RectInt => {
                                let left = rdr.read::<i32>(32)?;
                                let top = rdr.read::<i32>(32)?;
                                let right = rdr.read::<i32>(32)?;
                                let bottom = rdr.read::<i32>(32)?;
                                let data = KIDynamicFieldData::RectInt(RectInt {
                                    left,
                                    top,
                                    right,
                                    bottom,
                                });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::RectUInt => {
                                let left = rdr.read::<u32>(32)?;
                                let top = rdr.read::<u32>(32)?;
                                let right = rdr.read::<u32>(32)?;
                                let bottom = rdr.read::<u32>(32)?;
                                let data = KIDynamicFieldData::RectUInt(RectUInt {
                                    left,
                                    top,
                                    right,
                                    bottom,
                                });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::Matrix3x3 => {
                                let x1 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let y1 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let z1 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let x2 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let y2 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let z2 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let x3 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let y3 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let z3 = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let data = KIDynamicFieldData::Matrix3x3(Matrix3x3 {
                                    x1,
                                    y1,
                                    z1,
                                    x2,
                                    y2,
                                    z2,
                                    x3,
                                    y3,
                                    z3,
                                });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::SizeInt => {
                                let x = rdr.read::<i32>(32)?;
                                let y = rdr.read::<i32>(32)?;
                                let data = KIDynamicFieldData::SizeInt(SizeInt { x, y });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::SizeFloat => {
                                let x = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let y = f32::from_le_bytes(rdr.read::<u32>(32)?.to_le_bytes());
                                let data = KIDynamicFieldData::SizeFloat(SizeFloat { x, y });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::SizeUInt => {
                                let x = rdr.read::<u32>(32)?;
                                let y = rdr.read::<u32>(32)?;
                                let data = KIDynamicFieldData::SizeUInt(SizeUInt { x, y });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::UUniqueID => {
                                let val = rdr.read::<u128>(128)?;
                                let data = KIDynamicFieldData::UUniqueID(UUniqueID {
                                    inner: Uuid::from_u128_le(val),
                                });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::Color => {
                                let r = rdr.read::<u8>(8)?;
                                let g = rdr.read::<u8>(8)?;
                                let b = rdr.read::<u8>(8)?;
                                let a = rdr.read::<u8>(8)?;
                                let data = KIDynamicFieldData::Color(Color { r, g, b, a });
                                match field_data {
                                    KIDynamicFieldContainer::Single(v) => *v = data,
                                    KIDynamicFieldContainer::Vector(v) => v.push(data),
                                }
                            }
                            KIClassType::PirateNameIndices => {
                                //TODO
                                let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                                //output.create_unknown(field_id, data);
                            }
                            KIClassType::SerializeMap(key_id, value_id) => {
                                //TODO
                                let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                                //output.create_unknown(field_id, data);
                            },
                            KIClassType::SerializePair(a_id, b_id) => {
                                //TODO
                                let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                            }
                            KIClassType::Enum(eid) => {
                                if let Some(e) = self.classes.get_enum_by_hash(eid) {
                                    let data = if options.contains(SerializationOptions::VERSIONED_ENUM) {
                                        let value_name = self.read_string_enum_value(rdr, options)?;
                                        let ptr = self.classes.find_string(&value_name);
                                        if ptr.exists() {
                                            e.value_list
                                                .iter()
                                                .find_map(|v| {
                                                    if v.0 == ptr {
                                                        if let EnumValue::Value(data) = v.1 {
                                                            Some(KIDynamicFieldData::Enum(KIDynamicEnum {
                                                                value: data,
                                                                v_table: e.clone(),
                                                            }))
                                                        } else {
                                                            None
                                                        }
                                                    } else {
                                                        None
                                                    }
                                                })
                                                .unwrap_or_else(|| {
                                                    warn!("Failed to find enum value");
                                                    KIDynamicFieldData::String(value_name.into())
                                                })
                                        } else {
                                            KIDynamicFieldData::String(value_name.into())
                                        }
                                    } else {
                                        let value = self.read_numeric_enum_value(rdr, e.default_value)?;
                                        KIDynamicFieldData::Enum(KIDynamicEnum {
                                            value,
                                            v_table: e.clone(),
                                        })
                                    };
                                    match field_data {
                                        KIDynamicFieldContainer::Single(v) => *v = data,
                                        KIDynamicFieldContainer::Vector(v) => v.push(data),
                                    }
                                } else {
                                    let data = if options.contains(SerializationOptions::VERSIONED_ENUM) {
                                        let value_name = self.read_string_enum_value(rdr, options)?;
                                        KIDynamicFieldData::String(value_name.into())
                                    } else {
                                        KIDynamicFieldData::I32(rdr.read::<i32>(32)?)
                                    };
                                    match field_data {
                                        KIDynamicFieldContainer::Single(v) => *v = data,
                                        KIDynamicFieldContainer::Vector(v) => v.push(data),
                                    }
                                }
                            }
                            KIClassType::BitFlags(i) => {
                                if let Some(e) = self.classes.get_bitflags(i as usize) {
                                    let data = if options.contains(SerializationOptions::VERSIONED_ENUM) {
                                        let value_name = self.read_string_enum_value(rdr, options)?;
                                        let ptr = self.classes.find_string(&value_name);
                                        if ptr.exists() {
                                            e.value_list
                                                .iter()
                                                .find_map(|v| {
                                                    if v.0 == ptr {
                                                        if let EnumValue::Value(data) = v.1 {
                                                            Some(KIDynamicFieldData::NamelessEnum(
                                                                KIDynamicNamelessEnum {
                                                                    value: data,
                                                                    v_table: e.clone(),
                                                                },
                                                            ))
                                                        } else {
                                                            None
                                                        }
                                                    } else {
                                                        None
                                                    }
                                                })
                                                .unwrap_or_else(|| {
                                                    warn!("Failed to find enum value");
                                                    KIDynamicFieldData::String(value_name.into())
                                                })
                                        } else {
                                            KIDynamicFieldData::String(value_name.into())
                                        }
                                    } else {
                                        let value = self.read_numeric_enum_value(rdr, e.default_value)?;
                                        KIDynamicFieldData::NamelessEnum(KIDynamicNamelessEnum {
                                            value,
                                            v_table: e.clone(),
                                        })
                                    };
                                    match field_data {
                                        KIDynamicFieldContainer::Single(v) => *v = data,
                                        KIDynamicFieldContainer::Vector(v) => v.push(data),
                                    }
                                } else {
                                    let data = if options.contains(SerializationOptions::VERSIONED_ENUM) {
                                        let value_name = self.read_string_enum_value(rdr, options)?;
                                        KIDynamicFieldData::String(value_name.into())
                                    } else {
                                        KIDynamicFieldData::I32(rdr.read::<i32>(32)?)
                                    };
                                    match field_data {
                                        KIDynamicFieldContainer::Single(v) => *v = data,
                                        KIDynamicFieldContainer::Vector(v) => v.push(data),
                                    }
                                }
                            }
                            KIClassType::Class(id) => {
                                let class = self.read_class(rdr, options, unaligned)?;
                                match class {
                                    KIDynamicClassResult::Unknown { class_id, data } => {
                                        let data =
                                            KIDynamicFieldData::UnknownClass { class_id, data };
                                        match field_data {
                                            KIDynamicFieldContainer::Single(v) => *v = data,
                                            KIDynamicFieldContainer::Vector(v) => v.push(data),
                                        }
                                    }
                                    KIDynamicClassResult::Known(class) => {
                                        let data = KIDynamicFieldData::Class(class);
                                        match field_data {
                                            KIDynamicFieldContainer::Single(v) => *v = data,
                                            KIDynamicFieldContainer::Vector(v) => v.push(data),
                                        }
                                    }
                                    KIDynamicClassResult::None => {
                                        let data = if field.is_ptr() {
                                            KIDynamicFieldData::None
                                        } else if let Some(v) = self.classes.get_class_by_hash(id) {
                                            KIDynamicFieldData::Class(KIDynamicClass::new(
                                                v.clone(),
                                                self.classes(),
                                            ))
                                        } else {
                                            warn!(
                                                "Found a class field with a unknown class id? I presumed this to be impossible."
                                            );
                                            KIDynamicFieldData::UnknownClass {
                                                class_id: id,
                                                data: vec![],
                                            }
                                        };
                                        match field_data {
                                            KIDynamicFieldContainer::Single(v) => *v = data,
                                            KIDynamicFieldContainer::Vector(v) => v.push(data),
                                        }
                                    }
                                }
                            }
                            KIClassType::PirateNameIndices => {
                                //TODO
                                let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                                //output.create_unknown(field_id, data);
                            },
                            KIClassType::SerializeMap(key_id, value_id) => {
                                //TODO
                                let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                                //output.create_unknown(field_id, data);
                            },
                            KIClassType::SerializePair(a_id, b_id) => {
                                //TODO
                                let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                                //output.create_unknown(field_id, data);
                            }
                        }
                    }
                } else {
                    // println!(
                    //     "Did not find a field by the id: {} for class: {:?}({})",
                    //     field_id, class_name, class_id
                    // );
                    //println!("skipping: {}", field_size_in_bits);
                    //println!("bytes: {}", rdr.position_in_bits()? / 8);
                    let data = rdr.read_to_vec(field_size_in_bits.div_ceil(8) as usize)?;
                    output.create_unknown(field_id, data);
                    //rdr.skip(field_size_in_bits)?;
                }
                if field_size_in_bits % 8 != 0 {
                    *unaligned = true;
                }
                rdr.byte_align();
            }
            Ok(KIDynamicClassResult::Known(output))
        } else {
            let data = rdr.read_to_vec(class_size_in_bits.div_ceil(8) as usize)?;
            //println!("Failed to find a class with id: {}", class_id);

            Ok(KIDynamicClassResult::Unknown { class_id, data })
        }
    }

    fn read_container_size<B: AsRef<[u8]>>(
        &self,
        rdr: &mut BitReader<Cursor<B>, LittleEndian>,
        options: SerializationOptions,
    ) -> std::io::Result<u32> {
        if options.contains(SerializationOptions::COMPRESSED_INDEX) {
            let full_index = rdr.read::<u8>(1)? == 1;
            if !full_index {
                Ok(rdr.read::<u8>(7)? as u32)
            } else {
                rdr.read::<u32>(31)
            }
        } else {
            rdr.read::<u32>(32)
        }
    }

    fn read_string_size<B: AsRef<[u8]>>(
        &self,
        rdr: &mut BitReader<Cursor<B>, LittleEndian>,
        options: SerializationOptions,
    ) -> std::io::Result<u32> {
        if options.contains(SerializationOptions::COMPRESSED_INDEX) {
            let full_index = rdr.read::<u8>(1)? == 1;
            if !full_index {
                Ok(rdr.read::<u8>(7)? as u32)
            } else {
                rdr.read::<u32>(31)
            }
        } else {
            Ok(rdr.read::<u16>(16)? as u32)
        }
    }
}

#[derive(Clone)]
pub struct KIDynamicClass {
    v_table: Arc<KIClassLayout>,
    fields: Vec<KIDynamicFieldContainer>,
    unknown_fields: Vec<(u32, Vec<u8>)>,
}

impl std::fmt::Debug for KIDynamicClass {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let mut s = f.debug_struct(format!("KIDynamicClass({})", self.v_table.hash_id()).as_str());
        for field in self
            .fields
            .iter()
            .zip(self.v_table.fields().iter().map(|f| f.hash()))
        {
            s.field(field.1.to_string().as_str(), field.0);
        }
        for field in self.unknown_fields.iter() {
            s.field(format!("[UNKNOWN]{}", field.0).as_str(), &field.1);
        }
        s.finish()
    }
}

#[derive(Debug, Clone)]
pub enum KIDynamicClassResult {
    Unknown { class_id: u32, data: Vec<u8> },
    Known(KIDynamicClass),
    None,
}

impl KIDynamicClass {
    pub fn new(class_def: Arc<KIClassLayout>, class_table: &Arc<ClassData>) -> Self {
        let mut output = Self {
            fields: Vec::with_capacity(class_def.fields().len()),
            v_table: class_def.clone(),
            unknown_fields: Vec::with_capacity(0),
        };
        for field in class_def.fields() {
            output.create_field(field, class_table);
        }
        output
    }
    pub fn create_unknown(&mut self, field_hash: u32, data: Vec<u8>) {
        self.unknown_fields.push((field_hash, data));
    }

    pub fn create_field(&mut self, field: &KIClassFieldLayout, class_table: &Arc<ClassData>) {
        if !matches!(field.container(), Container::Single) {
            self.fields.push(KIDynamicFieldContainer::Vector(vec![]));
            return;
        }
        match field.ty() {
            KIClassType::Unknown => {
                warn!(
                    "Found a unknown field type with name: {:?}",
                    class_table.get_string(field.name)
                );
            }
            KIClassType::U8 => {
                if let Some(DefaultKIFieldValue::U8(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U8(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U8(0)));
                }
            }
            KIClassType::I8 => {
                if let Some(DefaultKIFieldValue::I8(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I8(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I8(0)));
                }
            }
            KIClassType::U16 => {
                if let Some(DefaultKIFieldValue::U16(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U16(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U16(0)));
                }
            }
            KIClassType::I16 => {
                if let Some(DefaultKIFieldValue::I16(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I16(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I16(0)));
                }
            }
            KIClassType::U32 => {
                if let Some(DefaultKIFieldValue::U32(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U32(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U32(0)));
                }
            }
            KIClassType::I32 => {
                if let Some(DefaultKIFieldValue::I32(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I32(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I32(0)));
                }
            }
            KIClassType::U64 => {
                if let Some(DefaultKIFieldValue::U64(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U64(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U64(0)));
                }
            }
            KIClassType::I64 => {
                if let Some(DefaultKIFieldValue::I64(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I64(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I64(0)));
                }
            }
            KIClassType::F32 => {
                if let Some(DefaultKIFieldValue::F32(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::F32(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::F32(
                            0.0,
                        )));
                }
            }
            KIClassType::F64 => {
                if let Some(DefaultKIFieldValue::F64(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::F64(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::F64(
                            0.0,
                        )));
                }
            }
            KIClassType::Bool => {
                if let Some(DefaultKIFieldValue::Bool(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::Bool(
                            *d,
                        )));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::Bool(
                            false,
                        )));
                }
            }
            KIClassType::String => {
                if let Some(DefaultKIFieldValue::String(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::String(
                            d.into(),
                        )));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::String(
                            "".into(),
                        )));
                }
            }
            KIClassType::WString => {
                if let Some(DefaultKIFieldValue::WString(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::WString(d.into()),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::WString("".into()),
                    ));
                }
            }
            KIClassType::Gid => {
                if let Some(DefaultKIFieldValue::U64(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U64(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U64(0)));
                }
            }
            KIClassType::BitInt(_) => {
                if let Some(DefaultKIFieldValue::BitInt(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U64(*d)));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::U64(0)));
                }
            }
            KIClassType::Vector3D => {
                if let Some(DefaultKIFieldValue::Vector3D(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::Vector3D(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::Vector3D(Vector3D::default()),
                    ));
                }
            }
            KIClassType::PointFloat => {
                if let Some(DefaultKIFieldValue::PointFloat(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::PointFloat(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::PointFloat(PointFloat::default()),
                    ));
                }
            }
            KIClassType::PointInt => {
                if let Some(DefaultKIFieldValue::PointInt(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::PointInt(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::PointInt(PointInt::default()),
                    ));
                }
            }
            KIClassType::PointUInt => {
                if let Some(DefaultKIFieldValue::PointUInt(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::PointUInt(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::PointUInt(PointUInt::default()),
                    ));
                }
            }
            KIClassType::RectFloat => {
                if let Some(DefaultKIFieldValue::RectFloat(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::RectFloat(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::RectFloat(RectFloat::default()),
                    ));
                }
            }
            KIClassType::RectInt => {
                if let Some(DefaultKIFieldValue::RectInt(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::RectInt(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::RectInt(RectInt::default()),
                    ));
                }
            }
            KIClassType::RectUInt => {
                if let Some(DefaultKIFieldValue::RectUInt(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::RectUInt(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::RectUInt(RectUInt::default()),
                    ));
                }
            }
            KIClassType::Matrix3x3 => {
                if let Some(DefaultKIFieldValue::Matrix3x3(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::Matrix3x3(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::Matrix3x3(Matrix3x3::default()),
                    ));
                }
            }
            KIClassType::SizeInt => {
                if let Some(DefaultKIFieldValue::SizeInt(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::SizeInt(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::SizeInt(SizeInt::default()),
                    ));
                }
            }
            KIClassType::SizeFloat => {
                if let Some(DefaultKIFieldValue::SizeFloat(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::SizeFloat(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::SizeFloat(SizeFloat::default()),
                    ));
                }
            }
            KIClassType::SizeUInt => {
                if let Some(DefaultKIFieldValue::SizeUInt(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::SizeUInt(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::SizeUInt(SizeUInt::default()),
                    ));
                }
            }
            KIClassType::UUniqueID => {
                if let Some(DefaultKIFieldValue::UUniqueID(d)) = field.default_value() {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::UUniqueID(*d),
                    ));
                } else {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::UUniqueID(UUniqueID::default()),
                    ));
                }
            }
            KIClassType::Color => {
                if let Some(DefaultKIFieldValue::Color(d)) = field.default_value() {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::Color(
                            *d,
                        )));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::Color(
                            Color::default(),
                        )));
                }
            }
            KIClassType::Enum(eid) => {
                if let Some(e) = class_table.get_enum_by_hash(eid) {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::Enum(
                            KIDynamicEnum {
                                value: e.default_value,
                                v_table: e.clone(),
                            },
                        )));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I32(0)));
                }
                //TODO
            }
            KIClassType::BitFlags(i) => {
                if let Some(e) = class_table.get_bitflags(i as usize) {
                    self.fields.push(KIDynamicFieldContainer::Single(
                        KIDynamicFieldData::NamelessEnum(KIDynamicNamelessEnum {
                            value: e.default_value,
                            v_table: e.clone(),
                        }),
                    ));
                } else {
                    self.fields
                        .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::I32(0)));
                }
                //TODO
            }
            KIClassType::Class(_) => {
                self.fields
                    .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::None));
                //TODO
            }
            KIClassType::SerializeMap(_, _) | KIClassType::SerializePair(_, _) => {
                self.fields
                    .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::None));
                //TODO
            }
            KIClassType::PirateNameIndices => {
                self.fields
                    .push(KIDynamicFieldContainer::Single(KIDynamicFieldData::None));
                //TODO
            }
        }
    }

    pub fn class_layout(&self) -> &Arc<KIClassLayout> {
        &self.v_table
    }

    pub fn fields(&self) -> &[KIDynamicFieldContainer] {
        &self.fields
    }

    pub fn unknown_fields(&self) -> &[(u32, Vec<u8>)] {
        self.unknown_fields.as_slice()
    }
}

#[derive(Debug, Clone)]
pub enum KIDynamicFieldContainer {
    Single(KIDynamicFieldData),
    Vector(Vec<KIDynamicFieldData>),
}

#[derive(Debug, Clone)]
pub struct KIDynamicEnum {
    pub value: EnumValueData,
    v_table: Arc<KIEnumLayout>,
}

impl KIDynamicEnum {
    pub fn enum_layout(&self) -> &Arc<KIEnumLayout> {
        &self.v_table
    }
}

#[derive(Debug, Clone)]
pub struct KIDynamicNamelessEnum {
    pub value: EnumValueData,
    v_table: Arc<KIBitFlagsLayout>,
}

impl KIDynamicNamelessEnum {
    pub fn enum_layout(&self) -> &Arc<KIBitFlagsLayout> {
        &self.v_table
    }
}

#[derive(Debug, Clone)]
pub enum KIDynamicFieldData {
    None,
    U8(u8),
    I8(i8),
    U16(u16),
    I16(i16),
    U32(u32),
    I32(i32),
    U64(u64),
    I64(i64),
    F32(f32),
    F64(f64),
    Bool(bool),
    Bytes(Vec<u8>),
    String(SmolStr),
    WString(SmolStr),
    Vector3D(Vector3D),
    PointFloat(PointFloat),
    PointInt(PointInt),
    PointUInt(PointUInt),
    RectFloat(RectFloat),
    RectInt(RectInt),
    RectUInt(RectUInt),
    Matrix3x3(Matrix3x3),
    SizeInt(SizeInt),
    SizeFloat(SizeFloat),
    SizeUInt(SizeUInt),
    UUniqueID(UUniqueID),
    Color(Color),
    Enum(KIDynamicEnum),
    NamelessEnum(KIDynamicNamelessEnum),
    Class(KIDynamicClass),
    UnknownClass { class_id: u32, data: Vec<u8> },
}
