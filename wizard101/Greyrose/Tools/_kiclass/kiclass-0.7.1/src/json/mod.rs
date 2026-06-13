use std::io::Cursor;

use log::warn;
use pyo3::{Bound, prelude::PyAnyMethods, types::PyList};
use serde_json::{Map, Value, json};
use smol_str::ToSmolStr;
use xml::writer::*;

use crate::{
    classes::{
        builtins::{
            Color, Matrix3x3, PointFloat, PointInt, PointUInt, RectFloat, RectInt, RectUInt,
            SizeFloat, SizeInt, SizeUInt, UUniqueID, Vector3D,
        },
        container::Container,
        flags::FieldFlags,
    },
    python_wrappers::{PyKIDynamicClass, UnknownClass},
};

pub fn class_to_json(class: &Bound<PyKIDynamicClass>) -> serde_json::Result<String> {
    let map = json_map_from_dyn(class);
    serde_json::to_string_pretty(&map)
}

pub fn class_to_xml(class: &Bound<PyKIDynamicClass>) -> Result<String, std::string::FromUtf8Error> {
    String::from_utf8(xml_from_dyn(class))
}

pub fn xml_from_dyn(class: &Bound<PyKIDynamicClass>) -> Vec<u8> {
    let mut wtr = EventWriter::new_with_config(
        Cursor::new(Vec::<u8>::new()),
        EmitterConfig::new()
            .normalize_empty_elements(true)
            .perform_indent(true)
            .write_document_declaration(false),
    );
    let element = XmlEvent::start_element("Objects");
    wtr.write(element).unwrap();
    write_class_to_xml(&mut wtr, class);
    wtr.write(XmlEvent::end_element()).unwrap();
    wtr.into_inner().into_inner()
}

fn write_class_to_xml(wtr: &mut EventWriter<Cursor<Vec<u8>>>, class: &Bound<PyKIDynamicClass>) {
    let c = class.borrow();
    let class_name = c.class_name();
    let start = XmlEvent::start_element("Class").attr("Name", class_name);
    wtr.write(start).unwrap();
    for field in c.class_layout().fields_with_flags(FieldFlags::SAVE) {
        let field_name = c.class_table().get_string(field.name()).unwrap();

        macro_rules! write_primitive_field {
            ($ty:ty) => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    wtr.write(
                        class
                            .getattr(field_name)
                            .unwrap()
                            .extract::<$ty>()
                            .unwrap()
                            .to_smolstr()
                            .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<$ty>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(val.to_smolstr().as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            };
        }

        match field.ty() {
            crate::classes::ty::KIClassType::Unknown => {
                warn!("Field type unknown within class: {:?}", field_name);
            }
            crate::classes::ty::KIClassType::U8 => write_primitive_field!(u8),
            crate::classes::ty::KIClassType::I8 => write_primitive_field!(i8),
            crate::classes::ty::KIClassType::U16 => write_primitive_field!(u16),
            crate::classes::ty::KIClassType::I16 => write_primitive_field!(i16),
            crate::classes::ty::KIClassType::U32 => write_primitive_field!(u32),
            crate::classes::ty::KIClassType::I32 => write_primitive_field!(i32),
            crate::classes::ty::KIClassType::U64 => write_primitive_field!(u64),
            crate::classes::ty::KIClassType::I64 => write_primitive_field!(i64),
            crate::classes::ty::KIClassType::F32 => write_primitive_field!(f32),
            crate::classes::ty::KIClassType::F64 => write_primitive_field!(f64),
            crate::classes::ty::KIClassType::Bool => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    wtr.write(
                        (class
                            .getattr(field_name)
                            .unwrap()
                            .extract::<bool>()
                            .unwrap() as u8)
                            .to_smolstr()
                            .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<bool>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write((val as u8).to_smolstr().as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::String => {
                if let Ok(field_data) = class.getattr(field_name) {
                    if field.container() == Container::Single {
                        if let Ok(val) = field_data.extract::<&str>() {
                            let element = XmlEvent::start_element(field_name);
                            wtr.write(element).unwrap();
                            if !val.is_empty() {
                                wtr.write(val).unwrap();
                            }
                            wtr.write(XmlEvent::end_element()).unwrap();
                        } else if field_data.extract::<Vec<u8>>().is_ok() {
                            warn!("String data is not a valid UTF-8 string, skipping for now");
                        } else {
                            warn!(
                                "Not a valid value type found for string: {:?}({:?})",
                                field_data.str(),
                                field_data.get_type().str()
                            );
                        }
                    } else if let Ok(data) = field_data.downcast::<PyList>() {
                        for val in data {
                            if let Ok(val) = val.extract::<&str>() {
                                let element = XmlEvent::start_element(field_name);
                                wtr.write(element).unwrap();
                                if !val.is_empty() {
                                    wtr.write(val).unwrap();
                                }
                                wtr.write(XmlEvent::end_element()).unwrap();
                            } else if val.extract::<Vec<u8>>().is_ok() {
                                warn!("String data is not a valid UTF-8 string, skipping for now");
                            } else {
                                warn!(
                                    "Not a valid value type found for string: {:?}({:?})",
                                    val.str(),
                                    val.get_type().str()
                                );
                            }
                        }
                    } else {
                        warn!("List data is mising");
                    }
                } else {
                    warn!("Field missing from class: {:?}", field_name);
                }
            }
            crate::classes::ty::KIClassType::WString => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let val = class.getattr(field_name).unwrap();
                    let val = val.extract::<&str>().unwrap();
                    if !val.is_empty() {
                        wtr.write(val).unwrap();
                    }
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<String>>()
                        .unwrap();
                    for val in data {
                        let element = XmlEvent::start_element(field_name);
                        wtr.write(element).unwrap();
                        if !val.is_empty() {
                            wtr.write(val.as_str()).unwrap();
                        }
                        wtr.write(XmlEvent::end_element()).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::Gid => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class.getattr(field_name).unwrap().extract::<u64>().unwrap();
                    wtr.write(format!("GID:{}", data).as_str()).unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u64>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("GID:{}", val).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::BitInt(_) => write_primitive_field!(u64),
            crate::classes::ty::KIClassType::Vector3D => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vector3D>()
                        .unwrap();
                    wtr.write(format!("{},{},{}", data.x, data.y, data.z).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<Vector3D>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{},{}", val.x, val.y, val.z).as_str())
                            .unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::PointFloat => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<PointFloat>()
                        .unwrap();
                    wtr.write(format!("{},{}", data.x, data.y).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<PointFloat>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{}", val.x, val.y).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::PointInt => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<PointInt>()
                        .unwrap();
                    wtr.write(format!("{},{}", data.x, data.y).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<PointInt>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{}", val.x, val.y).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::PointUInt => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<PointUInt>()
                        .unwrap();
                    wtr.write(format!("{},{}", data.x, data.y).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<PointUInt>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{}", val.x, val.y).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::RectFloat => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<RectFloat>()
                        .unwrap();
                    wtr.write(
                        format!(
                            "{},{},{},{}",
                            data.left, data.top, data.right, data.bottom
                        )
                        .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<RectFloat>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(
                            format!("{},{},{},{}", val.left, val.top, val.right, val.bottom)
                                .as_str(),
                        )
                        .unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::RectInt => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class.getattr(field_name).unwrap().extract::<RectInt>().unwrap();
                    wtr.write(
                        format!(
                            "{},{},{},{}",
                            data.left, data.top, data.right, data.bottom
                        )
                        .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<RectInt>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(
                            format!("{},{},{},{}", val.left, val.top, val.right, val.bottom)
                                .as_str(),
                        )
                        .unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::RectUInt => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class.getattr(field_name).unwrap().extract::<RectUInt>().unwrap();
                    wtr.write(
                        format!(
                            "{},{},{},{}",
                            data.left, data.top, data.right, data.bottom
                        )
                        .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<RectUInt>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(
                            format!("{},{},{},{}", val.left, val.top, val.right, val.bottom)
                                .as_str(),
                        )
                        .unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::Matrix3x3 => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Matrix3x3>()
                        .unwrap();
                    wtr.write(
                        format!(
                            "{},{},{},{},{},{},{},{},{}",
                            data.x1,
                            data.y1,
                            data.z1,
                            data.x2,
                            data.y2,
                            data.z2,
                            data.x3,
                            data.y3,
                            data.z3
                        )
                        .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<Matrix3x3>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(
                            format!(
                                "{},{},{},{},{},{},{},{},{}",
                                val.x1,
                                val.y1,
                                val.z1,
                                val.x2,
                                val.y2,
                                val.z2,
                                val.x3,
                                val.y3,
                                val.z3
                            )
                            .as_str(),
                        )
                        .unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::SizeInt => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<SizeInt>()
                        .unwrap();
                    wtr.write(format!("{},{}", data.x, data.y).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<SizeInt>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{}", val.x, val.y).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::SizeFloat => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<SizeFloat>()
                        .unwrap();
                    wtr.write(format!("{},{}", data.x, data.y).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<SizeFloat>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{}", val.x, val.y).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::SizeUInt => {
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<SizeUInt>()
                        .unwrap();
                    wtr.write(format!("{},{}", data.x, data.y).as_str())
                        .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<SizeUInt>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(format!("{},{}", val.x, val.y).as_str()).unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::UUniqueID => {
                //TODO: implement this
                warn!("Not yet implemented xml serialization for UUniqueID");
            }
            crate::classes::ty::KIClassType::Color => {
                //TODO: implement this
                if field.container() == Container::Single {
                    let element = XmlEvent::start_element(field_name);
                    wtr.write(element).unwrap();
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Color>()
                        .unwrap();
                    wtr.write(
                        format!(
                            "{3:02X}{0:02X}{1:02X}{2:02X}",
                            data.r, data.g, data.b, data.a
                        )
                        .as_str(),
                    )
                    .unwrap();
                    wtr.write(XmlEvent::end_element()).unwrap();
                } else {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<Color>>()
                        .unwrap();
                    for val in data {
                        let start = XmlEvent::start_element(field_name);
                        wtr.write(start).unwrap();
                        wtr.write(
                            format!("{3:02X}{0:02X}{1:02X}{2:02X}", val.r, val.g, val.b, val.a)
                                .as_str(),
                        )
                        .unwrap();
                        let end = XmlEvent::end_element();
                        wtr.write(end).unwrap();
                    }
                }
            }
            crate::classes::ty::KIClassType::Enum(eid) => {
                if field.container() == Container::Single {
                    let v = class.getattr(field_name).unwrap();
                    if let Ok(v) = v.extract::<i64>() {
                        if let Some(e) = c.class_table().get_enum_by_hash(eid) {
                            if let Some(v) = e.value_list.iter().find(|(_, e)| {
                                if let crate::classes::layout::EnumValue::Value(enum_value_data) = e
                                {
                                    match enum_value_data {
                                        crate::classes::layout::EnumValueData::U8(e) => {
                                            *e as i64 == v
                                        }
                                        crate::classes::layout::EnumValueData::I8(e) => {
                                            *e as i64 == v
                                        }
                                        crate::classes::layout::EnumValueData::U16(e) => {
                                            *e as i64 == v
                                        }
                                        crate::classes::layout::EnumValueData::I16(e) => {
                                            *e as i64 == v
                                        }
                                        crate::classes::layout::EnumValueData::U32(e) => {
                                            *e as i64 == v
                                        }
                                        crate::classes::layout::EnumValueData::I32(e) => {
                                            *e as i64 == v
                                        }
                                    }
                                } else {
                                    false
                                }
                            }) {
                                if let Some(v) = c.class_table().get_string(v.0) {
                                    let element = XmlEvent::start_element(field_name);
                                    wtr.write(element).unwrap();
                                    wtr.write(v).unwrap();
                                    wtr.write(XmlEvent::end_element()).unwrap();
                                } else {
                                    warn!(
                                        "Unexpected value: {:?}({:?}) within enum",
                                        v.1,
                                        c.class_table().get_string(e.name)
                                    );
                                }
                            }
                        }
                    } else if let Ok(v) = v.extract::<&str>() {
                        let element = XmlEvent::start_element(field_name);
                        wtr.write(element).unwrap();
                        wtr.write(v).unwrap();
                        wtr.write(XmlEvent::end_element()).unwrap();
                    } else {
                        warn!(
                            "Unexpected value: {:?}({:?}) within enum",
                            v.str(),
                            v.get_type().to_string()
                        );
                    }
                } else {
                    let list = class.getattr(field_name).unwrap();
                    let list = list.downcast::<PyList>().unwrap();
                    let iter = list.try_iter().unwrap();
                    for v in iter.flatten() {
                        if let Ok(v) = v.extract::<i64>() {
                            if let Some(e) = c.class_table().get_enum_by_hash(eid) {
                                if let Some(v) = e.value_list.iter().find(|(_, e)| {
                                    if let crate::classes::layout::EnumValue::Value(
                                        enum_value_data,
                                    ) = e
                                    {
                                        match enum_value_data {
                                            crate::classes::layout::EnumValueData::U8(e) => {
                                                *e as i64 == v
                                            }
                                            crate::classes::layout::EnumValueData::I8(e) => {
                                                *e as i64 == v
                                            }
                                            crate::classes::layout::EnumValueData::U16(e) => {
                                                *e as i64 == v
                                            }
                                            crate::classes::layout::EnumValueData::I16(e) => {
                                                *e as i64 == v
                                            }
                                            crate::classes::layout::EnumValueData::U32(e) => {
                                                *e as i64 == v
                                            }
                                            crate::classes::layout::EnumValueData::I32(e) => {
                                                *e as i64 == v
                                            }
                                        }
                                    } else {
                                        false
                                    }
                                }) {
                                    if let Some(v) = c.class_table().get_string(v.0) {
                                        let element = XmlEvent::start_element(field_name);
                                        wtr.write(element).unwrap();
                                        wtr.write(v).unwrap();
                                        wtr.write(XmlEvent::end_element()).unwrap();
                                    } else {
                                        warn!(
                                            "Unexpected value: {:?}({:?}) within enum",
                                            v.1,
                                            c.class_table().get_string(e.name)
                                        );
                                    }
                                }
                            }
                        } else if let Ok(v) = v.extract::<&str>() {
                            let element = XmlEvent::start_element(field_name);
                            wtr.write(element).unwrap();
                            wtr.write(v).unwrap();
                            wtr.write(XmlEvent::end_element()).unwrap();
                        } else {
                            warn!(
                                "Unexpected value: {:?}({:?}) within enum",
                                v.str(),
                                v.get_type().to_string()
                            );
                        }
                    }
                }
            }
            crate::classes::ty::KIClassType::BitFlags(eid) => {
                //TODO: implement this
                if field.container() == Container::Single {
                    let v = class.getattr(field_name).unwrap();
                    if let Ok(v) = v.extract::<i64>() {
                        if let Some(e) = c.class_table().get_bitflags(eid as usize) {
                            let mut output = String::with_capacity(32);
                            for v in e.value_list.iter().filter(|(_, e)| {
                                if let crate::classes::layout::EnumValue::Value(enum_value_data) = e
                                {
                                    match enum_value_data {
                                        crate::classes::layout::EnumValueData::U8(e) => {
                                            *e as i64 & v == v
                                        }
                                        crate::classes::layout::EnumValueData::I8(e) => {
                                            *e as i64 & v == v
                                        }
                                        crate::classes::layout::EnumValueData::U16(e) => {
                                            *e as i64 & v == v
                                        }
                                        crate::classes::layout::EnumValueData::I16(e) => {
                                            *e as i64 & v == v
                                        }
                                        crate::classes::layout::EnumValueData::U32(e) => {
                                            *e as i64 & v == v
                                        }
                                        crate::classes::layout::EnumValueData::I32(e) => {
                                            *e as i64 & v == v
                                        }
                                    }
                                } else {
                                    false
                                }
                            }) {
                                if let Some(v) = c.class_table().get_string(v.0) {
                                    if !output.is_empty() {
                                        output.push('|');
                                    }
                                    output.push_str(v);
                                } else {
                                    warn!("Unexpected value: {:?} within enum", v.1);
                                }
                            }
                            let element = XmlEvent::start_element(field_name);
                            wtr.write(element).unwrap();
                            wtr.write(output.as_str()).unwrap();
                            wtr.write(XmlEvent::end_element()).unwrap();
                        }
                    } else if let Ok(v) = v.extract::<&str>() {
                        let element = XmlEvent::start_element(field_name);
                        wtr.write(element).unwrap();
                        wtr.write(v).unwrap();
                        wtr.write(XmlEvent::end_element()).unwrap();
                    } else {
                        warn!(
                            "Unexpected value: {:?}({:?}) within enum",
                            v.str(),
                            v.get_type().to_string()
                        );
                    }
                } else {
                    let list = class.getattr(field_name).unwrap();
                    let list = list.downcast::<PyList>().unwrap();
                    let iter = list.try_iter().unwrap();
                    for v in iter.flatten() {
                        if let Ok(v) = v.extract::<i64>() {
                            if let Some(e) = c.class_table().get_bitflags(eid as usize) {
                                let mut output = String::with_capacity(32);
                                for v in e.value_list.iter().filter(|(_, e)| {
                                    if let crate::classes::layout::EnumValue::Value(
                                        enum_value_data,
                                    ) = e
                                    {
                                        match enum_value_data {
                                            crate::classes::layout::EnumValueData::U8(e) => {
                                                *e as i64 & v == v
                                            }
                                            crate::classes::layout::EnumValueData::I8(e) => {
                                                *e as i64 & v == v
                                            }
                                            crate::classes::layout::EnumValueData::U16(e) => {
                                                *e as i64 & v == v
                                            }
                                            crate::classes::layout::EnumValueData::I16(e) => {
                                                *e as i64 & v == v
                                            }
                                            crate::classes::layout::EnumValueData::U32(e) => {
                                                *e as i64 & v == v
                                            }
                                            crate::classes::layout::EnumValueData::I32(e) => {
                                                *e as i64 & v == v
                                            }
                                        }
                                    } else {
                                        false
                                    }
                                }) {
                                    if let Some(v) = c.class_table().get_string(v.0) {
                                        if !output.is_empty() {
                                            output.push('|');
                                        }
                                        output.push_str(v);
                                    } else {
                                        warn!("Unexpected value: {:?} within enum", v.1);
                                    }
                                }
                                let element = XmlEvent::start_element(field_name);
                                wtr.write(element).unwrap();
                                wtr.write(output.as_str()).unwrap();
                                wtr.write(XmlEvent::end_element()).unwrap();
                            } else {
                                warn!("Attempted to find a bitflag enum with no data: {:?}", eid);
                            }
                        } else if let Ok(v) = v.extract::<&str>() {
                            let element = XmlEvent::start_element(field_name);
                            wtr.write(element).unwrap();
                            wtr.write(v).unwrap();
                            wtr.write(XmlEvent::end_element()).unwrap();
                        } else {
                            warn!(
                                "Unexpected value: {:?}({:?}) within enum",
                                v.str(),
                                v.get_type().to_string()
                            );
                        }
                    }
                }
            }
            crate::classes::ty::KIClassType::Class(_) => {
                if field.container() == Container::Single {
                    let c = class.getattr(field_name).unwrap();
                    if c.is_none() {
                        //skip for now
                    } else if let Ok(v) = c.downcast::<PyKIDynamicClass>() {
                        wtr.write(XmlEvent::start_element(field_name)).unwrap();
                        write_class_to_xml(wtr, v);
                        wtr.write(XmlEvent::end_element()).unwrap();
                    } else if c.extract::<UnknownClass>().is_ok() || field.is_ptr() {
                        wtr.write(XmlEvent::start_element(field_name)).unwrap();
                        wtr.write(XmlEvent::end_element()).unwrap();
                    } else {
                        warn!(
                            "Unexpected value: {:?}({:?}) when expecting class",
                            c.str(),
                            c.get_type().to_string()
                        );
                    }
                } else {
                    let list = class.getattr(field_name).unwrap();
                    let list = list.downcast::<PyList>().unwrap();
                    let iter = list.try_iter().unwrap();
                    for v in iter.flatten() {
                        if let Ok(v) = v.downcast::<PyKIDynamicClass>() {
                            wtr.write(XmlEvent::start_element(field_name)).unwrap();
                            write_class_to_xml(wtr, v);
                            wtr.write(XmlEvent::end_element()).unwrap();
                        } else if v.extract::<UnknownClass>().is_ok() || field.is_ptr() {
                            wtr.write(XmlEvent::start_element(field_name)).unwrap();
                            wtr.write(XmlEvent::end_element()).unwrap();
                        } else {
                            warn!(
                                "Unexpected value: {:?}({:?}) within class vector",
                                v.str(),
                                v.get_type().to_string()
                            );
                        }
                    }
                }
            }
            crate::classes::ty::KIClassType::SerializePair(_, _) | crate::classes::ty::KIClassType::SerializeMap(_, _) | crate::classes::ty::KIClassType::PirateNameIndices => {
                //TODO
            }
        }
    }
    wtr.write(XmlEvent::end_element()).unwrap();
}

pub fn json_map_from_dyn(class: &Bound<PyKIDynamicClass>) -> Map<String, Value> {
    let c = class.borrow();
    let mut output = Map::with_capacity(c.class_layout().fields().len());
    output.insert("__class__".into(), c.class_name().into());
    for field in c.class_layout().fields() {
        let field_name = c.class_table().get_string(field.name()).unwrap();
        let value: Value = match field.ty() {
            crate::classes::ty::KIClassType::Unknown => {
                warn!(
                    "Unknown value: {:?} within class.",
                    c.class_table().get_string(field.name())
                );
                None::<()>.into()
            }
            crate::classes::ty::KIClassType::U8 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<u8>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u8>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::I8 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<i8>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<i8>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::U16 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<u16>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u16>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::I16 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<i16>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<i16>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::U32 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<u32>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u32>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::I32 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<i32>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<i32>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::U64 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<u64>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u64>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::I64 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<i64>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<i64>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::F32 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<f32>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<f32>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::F64 => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<f64>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<f64>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::Bool => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<bool>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<bool>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::String => {
                //TODO make this less wasteful
                if let Ok(field_data) = class.getattr(field_name) {
                    if field.container() == Container::Single {
                        if let Ok(val) = field_data.extract::<&str>() {
                            val.into()
                        } else if let Ok(val) = field_data.extract::<Vec<u8>>() {
                            val.into()
                        } else {
                            warn!(
                                "Not a valid value type found for string: {:?}({:?})",
                                field_data.str(),
                                field_data.get_type().str()
                            );
                            None::<()>.into()
                        }
                    } else if let Ok(val) = field_data.extract::<Vec<String>>() {
                        val.into()
                    } else if let Ok(val) = field_data.extract::<Vec<Vec<u8>>>() {
                        val.into()
                    } else if let Ok(data) = field_data.downcast::<PyList>() {
                        let mut output = Vec::with_capacity(data.len().unwrap_or_default());
                        for val in data {
                            if let Ok(v) = val.extract::<String>() {
                                output.push(Value::String(v));
                            } else if let Ok(v) = val.extract::<Vec<u8>>() {
                                output.push(v.into());
                            } else {
                                warn!(
                                    "Not a valid value type found for string: {:?}({:?})",
                                    val.str(),
                                    val.get_type().str()
                                );
                            }
                        }
                        Value::Array(output)
                    } else {
                        warn!("List data is mising");
                        Value::Array(Vec::with_capacity(0))
                    }
                } else {
                    warn!("Field missing from class: {:?}", field_name);
                    None::<()>.into()
                }
            }
            crate::classes::ty::KIClassType::WString => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<&str>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<String>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::Gid => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<u64>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u64>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::BitInt(_) => {
                if field.container() == Container::Single {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<u64>()
                        .unwrap()
                        .into()
                } else {
                    class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<u64>>()
                        .unwrap()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::Vector3D => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vector3D>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y, "z": data.z })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<Vector3D>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "x": x.x, "y": x.y, "z": x.z }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::PointFloat => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<PointFloat>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<PointFloat>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "x": x.x, "y": x.y }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::PointInt => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<PointInt>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<PointInt>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "x": x.x, "y": x.y }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::PointUInt => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<PointUInt>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<PointUInt>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "x": x.x, "y": x.y }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::RectFloat => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<RectFloat>()
                        .unwrap();
                    json!({ "left": data.left, "top": data.top, "right": data.right, "bottom": data.bottom })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<RectFloat>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "left": x.left, "top": x.top, "right": x.right, "bottom": x.bottom }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::RectInt => {
                if field.container() == Container::Single {
                    let data = class.getattr(field_name).unwrap().extract::<RectInt>().unwrap();
                    json!({ "left": data.left, "top": data.top, "right": data.right, "bottom": data.bottom })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<RectInt>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "left": x.left, "top": x.top, "right": x.right, "bottom": x.bottom }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::RectUInt => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<RectUInt>()
                        .unwrap();
                    json!({ "left": data.left, "top": data.top, "right": data.right, "bottom": data.bottom })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<RectUInt>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "left": x.left, "top": x.top, "right": x.right, "bottom": x.bottom }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::Matrix3x3 => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Matrix3x3>()
                        .unwrap();
                    json!({ "x1": data.x1, "y1": data.y1, "z1": data.z1, "x2": data.x2, "y2": data.y2, "z2": data.z2, "x3": data.x3, "y3": data.y3, "z3": data.z3 })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<Matrix3x3>>()
                        .unwrap();
                    data.drain(..).map(|x| json!({ "x1": x.x1, "y1": x.y1, "z1": x.z1, "x2": x.x2, "y2": x.y2, "z2": x.z2, "x3": x.x3, "y3": x.y3, "z3": x.z3 })).collect::<Vec<Value>>().into()
                }
            }
            crate::classes::ty::KIClassType::SizeInt => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<SizeInt>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<SizeInt>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "w": x.x, "h": x.y }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::SizeFloat => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<SizeFloat>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<SizeFloat>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "w": x.x, "h": x.y }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::SizeUInt => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<SizeUInt>()
                        .unwrap();
                    json!({ "x": data.x, "y": data.y })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<SizeUInt>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "w": x.x, "h": x.y }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::UUniqueID => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<UUniqueID>()
                        .unwrap();
                    data.inner.to_string().into()
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<UUniqueID>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| x.inner.to_string().into())
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::Color => {
                if field.container() == Container::Single {
                    let data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Color>()
                        .unwrap();
                    json!({ "r": data.r, "g": data.g, "b": data.b, "a": data.a })
                } else {
                    let mut data = class
                        .getattr(field_name)
                        .unwrap()
                        .extract::<Vec<Color>>()
                        .unwrap();
                    data.drain(..)
                        .map(|x| json!({ "r": x.r, "g": x.g, "b": x.b, "a": x.a }))
                        .collect::<Vec<Value>>()
                        .into()
                }
            }
            crate::classes::ty::KIClassType::Enum(_) => {
                if field.container() == Container::Single {
                    let v = class.getattr(field_name).unwrap();
                    if let Ok(v) = v.extract::<i64>() {
                        v.into()
                    } else if let Ok(v) = v.extract::<&str>() {
                        v.into()
                    } else {
                        warn!(
                            "Unexpected value: {:?}({:?}) within enum",
                            v.str(),
                            v.get_type().to_string()
                        );
                        0u64.into()
                    }
                } else {
                    let list = class.getattr(field_name).unwrap();
                    let list = list.downcast::<PyList>().unwrap();
                    let mut out = Vec::with_capacity(list.len().unwrap());
                    let iter = list.try_iter().unwrap();
                    for v in iter.flatten() {
                        if let Ok(v) = v.extract::<i64>() {
                            out.push(Value::from(v));
                        } else if let Ok(v) = v.extract::<&str>() {
                            out.push(Value::from(v));
                        } else {
                            warn!(
                                "Unexpected value: {:?}({:?}) within enum vector",
                                v.str(),
                                v.get_type().to_string()
                            );
                        }
                    }
                    out.into()
                }
            }
            crate::classes::ty::KIClassType::BitFlags(_) => {
                if field.container() == Container::Single {
                    let v = class.getattr(field_name).unwrap();
                    if let Ok(v) = v.extract::<i64>() {
                        v.into()
                    } else if let Ok(v) = v.extract::<&str>() {
                        v.into()
                    } else {
                        warn!(
                            "Unexpected value: {:?}({:?}) within enum",
                            v.str(),
                            v.get_type().to_string()
                        );
                        0u64.into()
                    }
                } else {
                    let list = class.getattr(field_name).unwrap();
                    let list = list.downcast::<PyList>().unwrap();
                    let mut out = Vec::with_capacity(list.len().unwrap());
                    let iter = list.try_iter().unwrap();
                    for v in iter.flatten() {
                        if let Ok(v) = v.extract::<i64>() {
                            out.push(Value::from(v));
                        } else if let Ok(v) = v.extract::<&str>() {
                            out.push(Value::from(v));
                        } else {
                            warn!(
                                "Unexpected value: {:?}({:?}) within enum vector",
                                v.str(),
                                v.get_type().to_string()
                            );
                        }
                    }
                    out.into()
                }
            }
            crate::classes::ty::KIClassType::Class(_) => {
                if field.container() == Container::Single {
                    let c = class.getattr(field_name).unwrap();
                    if c.is_none() {
                        None::<()>.into()
                    } else if let Ok(val) = c.downcast::<PyKIDynamicClass>() {
                        json_map_from_dyn(val).into()
                    } else if let Ok(val) = c.extract::<UnknownClass>() {
                        json!(
                            {
                                "__unknown__": true,
                                "__class__": val.id(),
                                "data": val.data()
                            }
                        )
                    } else if field.is_ptr() {
                        None::<()>.into()
                    } else {
                        warn!(
                            "Unexpected value: {:?}({:?}) when expecting class",
                            c.str(),
                            c.get_type().to_string()
                        );
                        None::<()>.into()
                    }
                } else {
                    let list = class.getattr(field_name).unwrap();
                    let list = list.downcast::<PyList>().unwrap();
                    let mut out = Vec::with_capacity(list.len().unwrap());
                    let iter = list.try_iter().unwrap();
                    for v in iter.flatten() {
                        if let Ok(v) = v.downcast::<PyKIDynamicClass>() {
                            out.push(Value::from(json_map_from_dyn(v)));
                        } else if let Ok(val) = v.extract::<UnknownClass>() {
                            out.push(json!(
                                {
                                    "__unknown__": true,
                                    "__class__": val.id(),
                                    "data": val.data()
                                }
                            ));
                        } else if field.is_ptr() {
                            out.push(None::<()>.into())
                        } else {
                            warn!(
                                "Unexpected value: {:?}({:?}) within class vector",
                                v.str(),
                                v.get_type().to_string()
                            );
                        }
                    }
                    out.into()
                }
            }
            crate::classes::ty::KIClassType::SerializePair(_, _) | crate::classes::ty::KIClassType::SerializeMap(_, _) => {
                //TODO
                warn!(
                    "Cannot serialize SerializePair or SerializeMap types yet: {:?}",
                    field_name
                );
                None::<()>.into()
            }
            crate::classes::ty::KIClassType::PirateNameIndices => {
                //TODO
                warn!(
                    "Cannot serialize SerializePair or SerializeMap types yet: {:?}",
                    field_name
                );
                None::<()>.into()
            }
        };
        output.insert(field_name.into(), value);
    }
    output
}
