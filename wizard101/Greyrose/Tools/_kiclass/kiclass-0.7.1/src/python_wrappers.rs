use std::{
    ops::{BitXor, Not},
    str::FromStr,
    sync::Arc,
};

use log::warn;

use pyo3::{
    Bound, IntoPyObject, IntoPyObjectExt, PyAny, PyErr, PyResult, Python,
    exceptions::PyOSError,
    intern,
    prelude::{PyAnyMethods, PyModule, PyModuleMethods, PyTypeMethods},
    pyclass, pyfunction, pymethods,
    types::{PyDict, PyList},
    wrap_pyfunction,
};

use crate::{
    bin_xml::deserialize::{KIBinDeserialize, KIDynamicClass, KIDynamicFieldData},
    classes::{
        ClassData,
        builtins::{
            Color, Matrix3x3, PointFloat, PointInt, PointUInt, RectFloat, RectInt, RectUInt,
            SizeFloat, SizeInt, SizeUInt, UUniqueID, Vector3D,
        },
        container::Container,
        default::DefaultKIFieldValue,
        flags::FieldFlags,
        layout::{EnumValue, EnumValueData, KIClassLayout},
        ty::KIClassType,
        xml::XMLtoClassBin,
    },
    hashing::{hash_string, light_hash_string},
    json::{class_to_json, class_to_xml},
    nav::NavFile,
    poi::PoiFile,
};

#[pyo3::pymodule]
pub fn kiclass(m: &Bound<'_, PyModule>) -> PyResult<()> {
    pyo3_log::init();
    m.add_function(wrap_pyfunction!(load_classes, m)?)?;
    m.add_function(wrap_pyfunction!(convert_xml_to_cdb, m)?)?;
    m.add_function(wrap_pyfunction!(merge_cdb_defs, m)?)?;
    m.add_function(wrap_pyfunction!(py_wiz_hash_string, m)?)?;
    m.add_function(wrap_pyfunction!(py_light_hash_string, m)?)?;
    m.add_function(wrap_pyfunction!(hash_field_type, m)?)?;
    m.add_function(wrap_pyfunction!(parse_nav, m)?)?;
    m.add_function(wrap_pyfunction!(parse_poi, m)?)?;
    m.add_class::<DynamicClassTable>()?;
    m.add_class::<FieldFlags>()?;
    m.add_class::<UnknownClass>()?;
    m.add_class::<KIBinaryFileReader>()?;
    m.add_class::<Vector3D>()?;
    m.add_class::<PointFloat>()?;
    m.add_class::<PointInt>()?;
    m.add_class::<PointUInt>()?;
    m.add_class::<RectFloat>()?;
    m.add_class::<RectInt>()?;
    m.add_class::<RectUInt>()?;
    m.add_class::<SizeInt>()?;
    m.add_class::<SizeUInt>()?;
    m.add_class::<SizeFloat>()?;
    m.add_class::<UUniqueID>()?;
    m.add_class::<Color>()?;
    m.add_class::<Matrix3x3>()?;
    m.add_class::<KIClassMode>()?;
    m.add_class::<PyNavFile>()?;
    m.add_class::<PyPoiFile>()?;
    Ok(())
}

#[pyclass(eq, eq_int, name = "Mode")]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum KIClassMode {
    Wizard,
    Pirate
}

#[pyfunction]
pub fn load_classes(client_defs: &str) -> PyResult<DynamicClassTable> {
    DynamicClassTable::new(client_defs)
}

#[pyfunction(signature = (path, output, mode = KIClassMode::Wizard))]
pub fn convert_xml_to_cdb(path: &str, output: &str, mode: KIClassMode) -> PyResult<()> {
    XMLtoClassBin::convert_from_xml(path, output, mode)?;
    Ok(())
}

#[pyfunction]
pub fn merge_cdb_defs(
    client_defs_path: &str,
    server_defs_path: &str,
    output_path: &str
) -> PyResult<()> {
    Ok(XMLtoClassBin::merge(
        client_defs_path,
        server_defs_path,
        output_path
    )?)
}

#[derive(Debug, Clone)]
#[pyclass(dict)]
pub struct PyKIDynamicClass(Arc<KIClassLayout>, Arc<ClassData>);

#[pymethods]
impl PyKIDynamicClass {
    pub fn class_name(&self) -> &str {
        self.1.get_string(self.0.name()).unwrap()
    }

    pub fn base_name(&self) -> Option<&str> {
        let base_id = self.0.base()?;
        let base_name = self.1.get_class_by_hash(base_id)?;
        self.1.get_string(base_name.name())
    }

    pub fn into_base(this: pyo3::Bound<Self>) -> PyResult<Option<Bound<Self>>> {
        let _this: Self = this.extract()?;
        let py = this.py();
        let Some(base_id) = _this.0.base() else {
            return Ok(None);
        };
        if let Some(base) = _this.1.get_class_by_hash(base_id) {
            let output = Self(base.clone(), _this.1.clone())
                .into_pyobject(py)
                .unwrap();
            for field in base.fields() {
                let field_name = _this.1.get_string(field.name()).unwrap();
                output.setattr(field_name, this.getattr(field_name)?)?;
            }
            Ok(Some(output))
        } else {
            Ok(None)
        }
    }

    pub fn __str__(this: pyo3::Bound<Self>) -> PyResult<Bound<PyAny>> {
        let dict = this.getattr(intern!(this.py(), "__dict__"))?;
        dict.call_method0(intern!(this.py(), "__str__"))
    }

    pub fn __repr__(this: pyo3::Bound<Self>) -> PyResult<Bound<PyAny>> {
        let dict = this.getattr(intern!(this.py(), "__dict__"))?;
        dict.call_method0(intern!(this.py(), "__repr__"))
    }

    pub fn __iter__(this: pyo3::Bound<Self>) -> PyResult<Bound<PyAny>> {
        let d = this
            .getattr(intern!(this.py(), "__dict__"))?
            .call_method0(intern!(this.py(), "items"))?;
        d.call_method0(intern!(this.py(), "__iter__"))
    }

    pub fn has_unknown_fields(this: pyo3::Bound<Self>) -> PyResult<bool> {
        let d = this.getattr(intern!(this.py(), "__unknownfields__"))?;
        let d = d.downcast::<PyDict>()?;
        Ok(d.len()? > 0)
    }

    pub fn unknown_fields(this: pyo3::Bound<Self>) -> PyResult<Bound<PyAny>> {
        this.getattr(intern!(this.py(), "__unknownfields__"))
    }

    pub fn field_flags(&self, field_name: &str) -> Option<FieldFlags> {
        let name_id = self.1.find_string(field_name);
        if !name_id.exists() {
            return None;
        }
        for field in self.0.fields() {
            if field.name() == name_id {
                return Some(field.flags());
            }
        }
        None
    }

    pub fn field_flags_by_index(&self, field_index: u32) -> Option<FieldFlags> {
        self.0
            .fields()
            .get(field_index as usize)
            .map(|field| field.flags())
    }

    pub fn is_known(&self) -> bool {
        true
    }

    pub fn base_class_iter(&self) -> BaseIterator {
        BaseIterator {
            class: self.0.clone(),
            classes: self.1.clone(),
        }
    }

    pub fn to_json(this: pyo3::Bound<Self>) -> PyResult<String> {
        Ok(class_to_json(&this).map_err(anyhow::Error::from)?)
    }

    pub fn to_xml(this: pyo3::Bound<Self>) -> PyResult<String> {
        Ok(class_to_xml(&this).map_err(anyhow::Error::from)?)
    }
}

impl PyKIDynamicClass {
    pub fn class_layout(&self) -> &Arc<KIClassLayout> {
        &self.0
    }

    pub fn class_table(&self) -> &Arc<ClassData> {
        &self.1
    }
}

#[derive(Debug, Clone)]
#[pyclass]
pub struct BaseIterator {
    class: Arc<KIClassLayout>,
    classes: Arc<ClassData>,
}

#[pymethods]
impl BaseIterator {
    pub fn __iter__(this: pyo3::Bound<Self>) -> Bound<Self> {
        this
    }

    pub fn __next__(&mut self) -> Option<&str> {
        if let Some(base) = self.class.base() {
            if let Some(class) = self.classes.get_class_by_hash(base) {
                self.class = class.clone();
                if let Some(name) = self.classes.get_string(class.name()) {
                    Some(name)
                } else {
                    None
                }
            } else {
                None
            }
        } else {
            None
        }
    }
}

#[pyclass]
pub struct DynamicClassTable {
    classes: Arc<ClassData>
}

#[pymethods]
impl DynamicClassTable {
    #[new]
    #[pyo3(signature = (class_defs))]
    pub fn new(class_defs: &str) -> PyResult<Self> {
        let class_data = XMLtoClassBin::load_from_file(class_defs)?;
        Ok(Self {
            classes: class_data.into()
        })
    }

    pub fn create_class_by_id<'py>(
        &self,
        py: Python<'py>,
        hash: u32,
    ) -> PyResult<Option<Bound<'py, PyKIDynamicClass>>> {
        let Some(class) = self.classes.get_class_by_hash(hash) else {
            warn!("Failed to find class with {}", hash);
            return Ok(None);
        };
        let output = PyKIDynamicClass(class.clone(), self.classes.clone())
            .into_pyobject(py)
            .unwrap();
        'main: for field in class.fields() {
            let field_name = self.classes.get_string(field.name()).unwrap();
            if field.container() != Container::Single {
                output.setattr(field_name, PyList::empty(py))?;
                continue;
            }
            match field.ty() {
                KIClassType::Unknown => panic!("Found a unknown field..."),
                KIClassType::U8 => {
                    if let Some(DefaultKIFieldValue::U8(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::I8 => {
                    if let Some(DefaultKIFieldValue::I8(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::U16 => {
                    if let Some(DefaultKIFieldValue::U16(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::I16 => {
                    if let Some(DefaultKIFieldValue::I16(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::U32 => {
                    if let Some(DefaultKIFieldValue::U32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::I32 => {
                    if let Some(DefaultKIFieldValue::I32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::U64 => {
                    if field_name.ends_with(".m_full") {
                        let field_name = field_name.split_once(".").unwrap();
                        if let Some(DefaultKIFieldValue::U64(def)) = field.default_value() {
                            output.setattr(field_name.0, def)?;
                        } else {
                            output.setattr(field_name.0, 0)?;
                        }
                    } else if let Some(DefaultKIFieldValue::U64(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::I64 => {
                    if let Some(DefaultKIFieldValue::I64(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::F32 => {
                    if let Some(DefaultKIFieldValue::F32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0.0)?;
                    }
                }
                KIClassType::F64 => {
                    if let Some(DefaultKIFieldValue::F64(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0.0)?;
                    }
                }
                KIClassType::Bool => {
                    if let Some(DefaultKIFieldValue::Bool(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, false)?;
                    }
                }
                KIClassType::String => {
                    if let Some(DefaultKIFieldValue::String(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, "")?;
                    }
                }
                KIClassType::WString => {
                    if let Some(DefaultKIFieldValue::WString(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, "")?;
                    }
                }
                KIClassType::Gid => {
                    if field_name.ends_with(".m_full") {
                        let field_name = field_name.split_once(".").unwrap();
                        if let Some(DefaultKIFieldValue::Gid(def)) = field.default_value() {
                            output.setattr(field_name.0, def)?;
                        } else {
                            output.setattr(field_name.0, 0)?;
                        }
                    } else if let Some(DefaultKIFieldValue::Gid(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::BitInt(_) => {
                    if let Some(DefaultKIFieldValue::BitInt(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::Vector3D => {
                    if let Some(DefaultKIFieldValue::Vector3D(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, Vector3D::default())?;
                    }
                }
                KIClassType::PointFloat => {
                    if let Some(DefaultKIFieldValue::PointFloat(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, PointFloat::default())?;
                    }
                }
                KIClassType::PointInt => {
                    if let Some(DefaultKIFieldValue::PointInt(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, PointInt::default())?;
                    }
                }
                KIClassType::PointUInt => {
                    if let Some(DefaultKIFieldValue::PointUInt(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, PointUInt::default())?;
                    }
                }
                KIClassType::RectFloat => {
                    if let Some(DefaultKIFieldValue::RectFloat(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, RectFloat::default())?;
                    }
                }
                KIClassType::RectInt => {
                    if let Some(DefaultKIFieldValue::RectInt(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, RectInt::default())?;
                    }
                }
                KIClassType::RectUInt => {
                    if let Some(DefaultKIFieldValue::RectUInt(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, RectUInt::default())?;
                    }
                }
                KIClassType::Matrix3x3 => {
                    if let Some(DefaultKIFieldValue::Matrix3x3(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, Matrix3x3::default())?;
                    }
                }
                KIClassType::SizeInt => {
                    if let Some(DefaultKIFieldValue::SizeInt(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, SizeInt::default())?;
                    }
                }
                KIClassType::SizeFloat => {
                    if let Some(DefaultKIFieldValue::SizeFloat(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, SizeFloat::default())?;
                    }
                }
                KIClassType::SizeUInt => {
                    if let Some(DefaultKIFieldValue::SizeUInt(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, SizeUInt::default())?;
                    }
                }
                KIClassType::UUniqueID => {
                    if let Some(DefaultKIFieldValue::UUniqueID(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, UUniqueID::default())?;
                    }
                }
                KIClassType::Color => {
                    if let Some(DefaultKIFieldValue::Color(def)) = field.default_value() {
                        output.setattr(field_name, *def)?;
                    } else {
                        output.setattr(field_name, Color::default())?;
                    }
                }
                KIClassType::SerializeMap(key_id, value_id) => {
                    //TODO
                    output.setattr(field_name, PyDict::new(py))?;
                    continue 'main;
                }
                KIClassType::SerializePair(a_id, b_id) => {
                    //TODO
                    output.setattr(field_name, (None::<()>))?;
                    continue 'main;
                }
                KIClassType::PirateNameIndices => {
                    //TODO
                    output.setattr(field_name, PyList::empty(py))?;
                    continue 'main;
                }
                KIClassType::Enum(eid) => {
                    if let Some(e) = self.classes.get_enum_by_hash(eid) {
                        let value =
                            if let Some(DefaultKIFieldValue::Enum(def)) = field.default_value() {
                                *def
                            } else {
                                EnumValueData::I32(0)
                            };
                        for (e_name, e_val) in e.value_list.iter() {
                            if let EnumValue::Value(e_val) = e_val {
                                if *e_val == value {
                                    if let Some(name) = self.classes.get_string(*e_name) {
                                        output.setattr(field_name, name)?;
                                        continue 'main;
                                    }
                                }
                            }
                        }
                        match value {
                            EnumValueData::U8(v) => output.setattr(field_name, v)?,
                            EnumValueData::I8(v) => output.setattr(field_name, v)?,
                            EnumValueData::U16(v) => output.setattr(field_name, v)?,
                            EnumValueData::I16(v) => output.setattr(field_name, v)?,
                            EnumValueData::U32(v) => output.setattr(field_name, v)?,
                            EnumValueData::I32(v) => output.setattr(field_name, v)?,
                        }
                    } else if let Some(DefaultKIFieldValue::I32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else if let Some(DefaultKIFieldValue::U32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::BitFlags(i) => {
                    if let Some(e) = self.classes.get_bitflags(i as usize) {
                        let value =
                            if let Some(DefaultKIFieldValue::Enum(def)) = field.default_value() {
                                *def
                            } else {
                                EnumValueData::I32(0)
                            };
                        for (e_name, e_val) in e.value_list.iter() {
                            if let EnumValue::Value(e_val) = e_val {
                                if *e_val == value {
                                    if let Some(name) = self.classes.get_string(*e_name) {
                                        output.setattr(field_name, name)?;
                                        continue 'main;
                                    }
                                }
                            }
                        }
                        match value {
                            EnumValueData::U8(v) => output.setattr(field_name, v)?,
                            EnumValueData::I8(v) => output.setattr(field_name, v)?,
                            EnumValueData::U16(v) => output.setattr(field_name, v)?,
                            EnumValueData::I16(v) => output.setattr(field_name, v)?,
                            EnumValueData::U32(v) => output.setattr(field_name, v)?,
                            EnumValueData::I32(v) => output.setattr(field_name, v)?,
                        }
                    } else if let Some(DefaultKIFieldValue::I32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else if let Some(DefaultKIFieldValue::U32(def)) = field.default_value() {
                        output.setattr(field_name, def)?;
                    } else {
                        output.setattr(field_name, 0)?;
                    }
                }
                KIClassType::Class(cid) => {
                    let c = self.create_class_by_id(py, cid)?;
                    output.setattr(field_name, c)?;
                }
            }
        }
        Ok(Some(output))
    }

    pub fn create_class<'py>(
        &self,
        py: Python<'py>,
        class: &str,
    ) -> PyResult<Option<Bound<'py, PyKIDynamicClass>>> {
        let hash = if KIClassMode::Wizard == self.classes.mode() {
            light_hash_string(class.as_bytes())
        } else {
            hash_string(class.as_bytes())
        };
        self.create_class_by_id(py, hash)
    }
}

fn from_dyn_class<'py>(
    this: &Arc<ClassData>,
    py: &Python<'py>,
    class: &KIDynamicClass,
) -> PyResult<Option<Bound<'py, PyKIDynamicClass>>> {
    let output = PyKIDynamicClass(class.class_layout().clone(), this.clone())
        .into_pyobject(*py)
        .unwrap();
    'main: for (field_layout, field_data) in
        class.class_layout().fields().iter().zip(class.fields())
    {
        let field_name = this.get_string(field_layout.name()).unwrap();
        match field_data {
            crate::bin_xml::deserialize::KIDynamicFieldContainer::Single(val) => match val {
                crate::bin_xml::deserialize::KIDynamicFieldData::None => {
                    output.setattr(field_name, None::<()>)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::U8(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::I8(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::U16(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::I16(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::U32(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::I32(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::U64(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::I64(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::F32(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::F64(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::Bool(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::Bytes(val) => {
                    output.setattr(field_name, val.clone())?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::String(val) => {
                    output.setattr(field_name, val.to_string())?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::WString(val) => {
                    output.setattr(field_name, val.to_string())?;
                }
                KIDynamicFieldData::Vector3D(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::PointFloat(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::PointInt(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::PointUInt(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::RectFloat(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::RectInt(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::RectUInt(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::Matrix3x3(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::SizeInt(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::SizeFloat(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::SizeUInt(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::UUniqueID(val) => {
                    output.setattr(field_name, *val)?;
                }
                KIDynamicFieldData::Color(val) => {
                    output.setattr(field_name, *val)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::Class(val) => {
                    output.setattr(field_name, from_dyn_class(this, py, val)?)?;
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::Enum(val) => {
                    for (e_name, e_val) in val.enum_layout().value_list.iter() {
                        if let EnumValue::Value(e_val) = e_val {
                            if *e_val == val.value {
                                if let Some(name) = this.get_string(*e_name) {
                                    output.setattr(field_name, name)?;
                                    continue 'main;
                                }
                            }
                        }
                    }
                    match val.value {
                        crate::classes::layout::EnumValueData::U8(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::I8(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::U16(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::I16(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::U32(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::I32(v) => {
                            output.setattr(field_name, v)?
                        }
                    }
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::NamelessEnum(val) => {
                    for (e_name, e_val) in val.enum_layout().value_list.iter() {
                        if let EnumValue::Value(e_val) = e_val {
                            if *e_val == val.value {
                                if let Some(name) = this.get_string(*e_name) {
                                    output.setattr(field_name, name)?;
                                    continue 'main;
                                }
                            }
                        }
                    }
                    match val.value {
                        crate::classes::layout::EnumValueData::U8(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::I8(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::U16(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::I16(v) => {
                            output.setattr(field_name, v)?
                        }

                        crate::classes::layout::EnumValueData::U32(v) => {
                            output.setattr(field_name, v)?
                        }
                        crate::classes::layout::EnumValueData::I32(v) => {
                            output.setattr(field_name, v)?
                        }
                    }
                }
                crate::bin_xml::deserialize::KIDynamicFieldData::UnknownClass {
                    class_id,
                    data,
                } => {
                    output.setattr(
                        field_name,
                        UnknownClass {
                            class_id: *class_id,
                            data: data.clone(),
                        }
                        .into_bound_py_any(*py)?,
                    )?;
                }
            },
            crate::bin_xml::deserialize::KIDynamicFieldContainer::Vector(val) => {
                let mut list = vec![];
                for val in val.iter() {
                    match val {
                        crate::bin_xml::deserialize::KIDynamicFieldData::None => {
                            list.push(None::<()>.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::U8(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::I8(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::U16(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::I16(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::U32(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::I32(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::U64(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::I64(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::F32(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::F64(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::Bool(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::Bytes(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::String(val) => {
                            list.push(val.to_string().into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::WString(val) => {
                            list.push(val.to_string().into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::Vector3D(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::PointFloat(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::PointInt(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::PointUInt(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::RectFloat(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::RectInt(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::RectUInt(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::Matrix3x3(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::SizeInt(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::SizeFloat(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::SizeUInt(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::UUniqueID(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        KIDynamicFieldData::Color(val) => {
                            list.push(val.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::Class(val) => {
                            list.push(from_dyn_class(this, py, val)?.into_bound_py_any(*py)?);
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::Enum(val) => {
                            let mut matched_named_value = false;
                            for (e_name, e_val) in val.enum_layout().value_list.iter() {
                                if let EnumValue::Value(e_val) = e_val {
                                    if *e_val == val.value {
                                        if let Some(name) = this.get_string(*e_name) {
                                            list.push(name.into_bound_py_any(*py)?);
                                            matched_named_value = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if !matched_named_value {
                                match val.value {
                                    crate::classes::layout::EnumValueData::U8(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::I8(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::U16(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::I16(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::U32(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::I32(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                }
                            }
                        }
                        KIDynamicFieldData::NamelessEnum(val) => {
                            let mut matched_named_value = false;
                            for (e_name, e_val) in val.enum_layout().value_list.iter() {
                                if let EnumValue::Value(e_val) = e_val {
                                    if *e_val == val.value {
                                        if let Some(name) = this.get_string(*e_name) {
                                            list.push(name.into_bound_py_any(*py)?);
                                            matched_named_value = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if !matched_named_value {
                                match val.value {
                                    crate::classes::layout::EnumValueData::U8(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::I8(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::U16(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::I16(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::U32(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                    crate::classes::layout::EnumValueData::I32(v) => {
                                        list.push(v.into_bound_py_any(*py)?)
                                    }
                                }
                            }
                        }
                        crate::bin_xml::deserialize::KIDynamicFieldData::UnknownClass {
                            class_id,
                            data,
                        } => list.push(
                            UnknownClass {
                                class_id: *class_id,
                                data: data.clone(),
                            }
                            .into_bound_py_any(*py)?,
                        ),
                    }
                }
                output.setattr(field_name, list)?;
            }
        }
    }
    let dict = PyDict::new(*py);
    for unknown in class.unknown_fields() {
        dict.set_item(unknown.0, unknown.1.clone())?;
    }
    output.setattr(intern!(*py, "__unknownfields__"), dict)?;
    Ok(Some(output))
}

#[derive(Debug, Clone)]
#[pyclass]
pub struct UnknownClass {
    class_id: u32,
    data: Vec<u8>,
}

#[pymethods]
impl UnknownClass {
    pub fn id(&self) -> u32 {
        self.class_id
    }

    pub fn data(&self) -> &[u8] {
        &self.data
    }

    pub fn is_known(&self) -> bool {
        false
    }

    pub fn __str__(&self) -> String {
        format!("{{'class_id': {}, 'data': ..}}", self.class_id)
    }

    pub fn __repr__(&self) -> String {
        format!("{{'class_id': {}, 'data':{:X?}}}", self.class_id, self.data)
    }
}

#[derive(Debug, Clone)]
pub struct InvalidTypeError {
    expected: String,
    found: String,
}

impl std::fmt::Display for InvalidTypeError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "expected {} but found: {}", self.expected, self.found)
    }
}

impl std::error::Error for InvalidTypeError {}

impl std::convert::From<InvalidTypeError> for PyErr {
    fn from(err: InvalidTypeError) -> PyErr {
        PyOSError::new_err(err.to_string())
    }
}

#[pymethods]
impl FieldFlags {
    #[new]
    #[pyo3(signature = (value=None))]
    pub fn new(value: Option<Bound<'_, PyAny>>) -> anyhow::Result<Self> {
        if let Some(value) = value {
            if let Ok(v) = value.extract::<u32>() {
                Ok(FieldFlags::from_bits_retain(v))
            } else if let Ok(v) = value.extract::<&str>() {
                Ok(FieldFlags::from_str(v)?)
            } else if let Ok(v) = value.extract::<FieldFlags>() {
                Ok(v)
            } else {
                anyhow::bail!(InvalidTypeError {
                    expected: "FieldFlags|string|int".into(),
                    found: value.get_type().name().unwrap().to_string(),
                })
            }
        } else {
            Ok(FieldFlags::empty())
        }
    }

    #[staticmethod]
    pub fn save() -> Self {
        Self::SAVE
    }

    #[staticmethod]
    pub fn copy() -> Self {
        Self::COPY
    }

    #[staticmethod]
    pub fn public() -> Self {
        Self::PUBLIC
    }

    #[staticmethod]
    pub fn transmitplayer() -> Self {
        Self::TRANSMITPLAYER
    }

    #[staticmethod]
    pub fn transmitcsr() -> Self {
        Self::TRANSMITCSR
    }

    #[staticmethod]
    pub fn persist() -> Self {
        Self::PERSIST
    }

    #[staticmethod]
    pub fn deprecated() -> Self {
        Self::DEPRECATED
    }

    #[staticmethod]
    pub fn noscript() -> Self {
        Self::NOSCRIPT
    }

    #[staticmethod]
    pub fn deltasave() -> Self {
        Self::DELTA_SAVE
    }

    #[staticmethod]
    pub fn binary() -> Self {
        Self::BINARY
    }

    #[staticmethod]
    #[allow(clippy::should_implement_trait)]
    pub fn default() -> Self {
        Self::DEFAULT
    }

    #[staticmethod]
    pub fn transmit() -> Self {
        Self::TRANSMIT
    }

    #[staticmethod]
    pub fn noedit() -> Self {
        Self::NOEDIT
    }

    #[staticmethod]
    pub fn filename() -> Self {
        Self::FILENAME
    }

    #[staticmethod]
    pub fn color() -> Self {
        Self::COLOR
    }

    #[staticmethod]
    pub fn range() -> Self {
        Self::RANGE
    }

    #[staticmethod]
    #[pyo3(name = "bits")]
    pub fn _bits() -> Self {
        Self::BITS
    }

    #[staticmethod]
    #[pyo3(name = "enum")]
    pub fn _enum() -> Self {
        Self::ENUM
    }

    #[staticmethod]
    pub fn localized() -> Self {
        Self::LOCALIZED
    }

    #[staticmethod]
    pub fn stringkey() -> Self {
        Self::STRINGKEY
    }

    #[staticmethod]
    pub fn objectid() -> Self {
        Self::OBJECTID
    }

    #[staticmethod]
    pub fn referenceid() -> Self {
        Self::REFERENCEID
    }

    #[staticmethod]
    pub fn radians() -> Self {
        Self::RADIANS
    }

    #[staticmethod]
    pub fn name() -> Self {
        Self::NAME
    }

    #[staticmethod]
    pub fn nameref() -> Self {
        Self::NAMEREF
    }

    #[staticmethod]
    #[pyo3(name = "override")]
    pub fn _override() -> Self {
        Self::OVERRIDE
    }

    #[staticmethod]
    pub fn weak() -> Self {
        Self::WEAK
    }

    #[staticmethod]
    pub fn editormask() -> Self {
        Self::EDITORMASK
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __eq__(&self, other: Bound<'_, PyAny>) -> bool {
        if let Ok(flags) = other.extract::<u32>() {
            flags == self.bits()
        } else if let Ok(flags) = other.extract::<Self>() {
            flags == *self
        } else {
            false
        }
    }

    pub fn __and__(&self, other: Bound<'_, PyAny>) -> Result<Self, InvalidTypeError> {
        if let Ok(flags) = other.extract::<u32>() {
            Ok(Self::from_bits_retain(self.bits() & flags))
        } else if let Ok(flags) = other.extract::<Self>() {
            Ok(*self & flags)
        } else {
            Err(InvalidTypeError {
                expected: "FieldFlags|int".into(),
                found: other.get_type().name().unwrap().to_string(),
            })
        }
    }

    pub fn __or__(&self, other: Bound<'_, PyAny>) -> Result<Self, InvalidTypeError> {
        if let Ok(flags) = other.extract::<u32>() {
            Ok(Self::from_bits_retain(self.bits() | flags))
        } else if let Ok(flags) = other.extract::<Self>() {
            Ok(*self | flags)
        } else {
            Err(InvalidTypeError {
                expected: "FieldFlags|int".into(),
                found: other.get_type().name().unwrap().to_string(),
            })
        }
    }

    pub fn __xor__(&self, other: Bound<'_, PyAny>) -> Result<Self, InvalidTypeError> {
        if let Ok(flags) = other.extract::<u32>() {
            Ok(Self::from_bits_retain(self.bits().bitxor(flags)))
        } else if let Ok(flags) = other.extract::<Self>() {
            Ok(self.bitxor(flags))
        } else {
            Err(InvalidTypeError {
                expected: "FieldFlags|int".into(),
                found: other.get_type().name().unwrap().to_string(),
            })
        }
    }

    pub fn __invert__(&self) -> Self {
        self.not()
    }

    pub fn __int__(&self) -> u32 {
        self.bits()
    }
}

impl FromStr for FieldFlags {
    //TODO: convert this to its own type
    type Err = anyhow::Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let split = s.split('|');
        let mut output = FieldFlags::empty();
        for split in split {
            match split.trim() {
                "SAVE" => output |= FieldFlags::SAVE,
                "COPY" => output |= FieldFlags::COPY,
                "PUBLIC" => output |= FieldFlags::PUBLIC,
                "TRANSMITPLAYER" => output |= FieldFlags::TRANSMITPLAYER,
                "TRANSMITCSR" => output |= FieldFlags::TRANSMITCSR,
                "PERSIST" => output |= FieldFlags::PERSIST,
                "DEPRECATED" => output |= FieldFlags::DEPRECATED,
                "NOSCRIPT" => output |= FieldFlags::NOSCRIPT,
                "DELTA_SAVE" => output |= FieldFlags::DELTA_SAVE,
                "BINARY" => output |= FieldFlags::BINARY,
                "DEFAULT" => output |= FieldFlags::DEFAULT,
                "TRANSMIT" => output |= FieldFlags::TRANSMIT,
                "NOEDIT" => output |= FieldFlags::NOEDIT,
                "FILENAME" => output |= FieldFlags::FILENAME,
                "COLOR" => output |= FieldFlags::COLOR,
                "RANGE" => output |= FieldFlags::RANGE,
                "BITS" => output |= FieldFlags::BITS,
                "ENUM" => output |= FieldFlags::ENUM,
                "LOCALIZED" => output |= FieldFlags::LOCALIZED,
                "STRINGKEY" => output |= FieldFlags::STRINGKEY,
                "OBJECTID" => output |= FieldFlags::OBJECTID,
                "REFERENCEID" => output |= FieldFlags::REFERENCEID,
                "RADIANS" => output |= FieldFlags::RADIANS,
                "NAME" => output |= FieldFlags::NAME,
                "NAMEREF" => output |= FieldFlags::NAMEREF,
                "OVERRIDE" => output |= FieldFlags::OVERRIDE,
                "WEAK" => output |= FieldFlags::WEAK,
                "EDITORMASK" => output |= FieldFlags::EDITORMASK,
                v => {
                    anyhow::bail!("Received an invalid flag by name of: {}", v);
                }
            }
        }
        Ok(output)
    }
}

#[pyfunction(signature = (field_name, field_type_name, mode = KIClassMode::Wizard))]
pub fn hash_field_type(field_name: &str, field_type_name: &str, mode: KIClassMode) -> u32 {
    match mode {
        KIClassMode::Wizard => hash_string(field_type_name.as_bytes()) + light_hash_string(field_name.as_bytes()),
        KIClassMode::Pirate => light_hash_string(field_type_name.as_bytes()) + light_hash_string(field_name.as_bytes()),
    }
}

#[pyfunction]
#[pyo3(name = "light_hash_string")]
pub fn py_light_hash_string(input: &str) -> u32 {
    light_hash_string(input.as_bytes())
}

#[pyfunction]
#[pyo3(name = "wiz_hash_string")]
pub fn py_wiz_hash_string(input: &str) -> u32 {
    hash_string(input.as_bytes())
}

#[derive(Clone)]
#[pyclass]
pub struct KIBinaryFileReader(KIBinDeserialize);

impl KIBinaryFileReader {
    fn to_python_result<'py>(
        &self,
        py: Python<'py>,
        class: crate::bin_xml::deserialize::KIDynamicClassResult,
    ) -> PyResult<Option<Bound<'py, PyAny>>> {
        match class {
            crate::bin_xml::deserialize::KIDynamicClassResult::Unknown { class_id, data } => {
                Ok(Some(UnknownClass { class_id, data }.into_bound_py_any(py)?))
            }
            crate::bin_xml::deserialize::KIDynamicClassResult::Known(class) => {
                if let Some(v) = from_dyn_class(self.0.classes(), &py, &class)? {
                    Ok(Some(v.into_any()))
                } else {
                    Ok(None)
                }
            }
            crate::bin_xml::deserialize::KIDynamicClassResult::None => Ok(None),
        }
    }
}

#[pymethods]
impl KIBinaryFileReader {
    #[new]
    pub fn new(table: &DynamicClassTable) -> Self {
        Self(KIBinDeserialize::new(table.classes.clone()))
    }

    pub fn read_file<'py>(
        &self,
        py: Python<'py>,
        path: &str,
    ) -> PyResult<Option<Bound<'py, PyAny>>> {
        let class = self.0.read_file(path)?;
        self.to_python_result(py, class)
    }

    #[pyo3(signature = (path, options = 0))]
    pub fn read_raw_file<'py>(
        &self,
        py: Python<'py>,
        path: &str,
        options: u32,
    ) -> PyResult<Option<Bound<'py, PyAny>>> {
        let class = self.0.read_raw_file(
            path,
            crate::bin_xml::deserialize::SerializationOptions::from_bits_retain(options),
        )?;
        self.to_python_result(py, class)
    }
}

// ── Nav file support ────────────────────────────────────────────────────────

#[pyclass(name = "NavFile")]
pub struct PyNavFile {
    inner: NavFile,
}

#[pymethods]
impl PyNavFile {
    pub fn to_json(&self) -> PyResult<String> {
        self.inner.to_json()
            .map_err(|e| PyErr::new::<pyo3::exceptions::PyRuntimeError, _>(e.to_string()))
    }

    #[getter]
    pub fn format(&self) -> &str {
        match &self.inner {
            NavFile::ZoneMap(_) => "zone_map",
            NavFile::ZoneNav(_) => "zone_nav",
        }
    }

    #[getter]
    pub fn node_count(&self) -> usize {
        match &self.inner {
            NavFile::ZoneMap(m) => m.nodes.len(),
            NavFile::ZoneNav(m) => m.nodes.len(),
        }
    }

    #[getter]
    pub fn edge_count(&self) -> usize {
        match &self.inner {
            NavFile::ZoneMap(m) => m.edges.len(),
            NavFile::ZoneNav(m) => m.edges.len(),
        }
    }
}

#[pyfunction]
pub fn parse_nav(path: &str) -> PyResult<PyNavFile> {
    NavFile::from_file(path)
        .map(|inner| PyNavFile { inner })
        .map_err(|e| PyErr::new::<pyo3::exceptions::PyIOError, _>(e.to_string()))
}

// ── POI file bindings ─────────────────────────────────────────────────────────

#[pyclass(name = "PoiFile")]
pub struct PyPoiFile {
    inner: PoiFile,
}

#[pymethods]
impl PyPoiFile {
    /// Return the full parsed data as a pretty-printed JSON string.
    pub fn to_json(&self) -> String {
        self.inner.to_json()
    }

    #[getter]
    pub fn zone_count(&self) -> usize {
        self.inner.zone_names.len()
    }

    #[getter]
    pub fn goal_count(&self) -> usize {
        self.inner.goals.len()
    }

    #[getter]
    pub fn teleporter_zone_count(&self) -> usize {
        self.inner.teleporters.len()
    }

    #[getter]
    pub fn zone_mob_count(&self) -> usize {
        self.inner.zone_mobs.len()
    }
}

/// Parse a `poi.dat` file and return a :class:`PoiFile` object.
#[pyfunction]
pub fn parse_poi(path: &str) -> PyResult<PyPoiFile> {
    PoiFile::from_file(path)
        .map(|inner| PyPoiFile { inner })
        .map_err(|e| PyErr::new::<pyo3::exceptions::PyIOError, _>(e.to_string()))
}
