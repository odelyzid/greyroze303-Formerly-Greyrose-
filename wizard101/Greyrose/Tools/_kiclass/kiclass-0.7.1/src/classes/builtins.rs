use pyo3::{pyclass, pymethods};
use serde::{Deserialize, Serialize};
use std::{
    convert::TryFrom,
    fmt::{self, Display},
};
use uuid::Uuid;

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct PointUInt {
    #[pyo3(get, set)]
    pub x: u32,
    #[pyo3(get, set)]
    pub y: u32,
}

#[pymethods]
impl PointUInt {
    #[new]
    pub fn new(x: u32, y: u32) -> Self {
        Self { x, y }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct SizeUInt {
    #[pyo3(get, set)]
    pub x: u32,
    #[pyo3(get, set)]
    pub y: u32,
}

#[pymethods]
impl SizeUInt {
    #[new]
    pub fn new(x: u32, y: u32) -> Self {
        Self { x, y }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct PointInt {
    #[pyo3(get, set)]
    pub x: i32,
    #[pyo3(get, set)]
    pub y: i32,
}

#[pymethods]
impl PointInt {
    #[new]
    pub fn new(x: i32, y: i32) -> Self {
        Self { x, y }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct SizeInt {
    #[pyo3(get, set)]
    pub x: i32,
    #[pyo3(get, set)]
    pub y: i32,
}

#[pymethods]
impl SizeInt {
    #[new]
    pub fn new(x: i32, y: i32) -> Self {
        Self { x, y }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct PointFloat {
    #[pyo3(get, set)]
    pub x: f32,
    #[pyo3(get, set)]
    pub y: f32,
}

#[pymethods]
impl PointFloat {
    #[new]
    pub fn new(x: f32, y: f32) -> Self {
        Self { x, y }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct SizeFloat {
    #[pyo3(get, set)]
    pub x: f32,
    #[pyo3(get, set)]
    pub y: f32,
}

#[pymethods]
impl SizeFloat {
    #[new]
    pub fn new(x: f32, y: f32) -> Self {
        Self { x, y }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct RectInt {
    #[pyo3(get, set)]
    pub left: i32,
    #[pyo3(get, set)]
    pub top: i32,
    #[pyo3(get, set)]
    pub right: i32,
    #[pyo3(get, set)]
    pub bottom: i32,
}

#[pymethods]
impl RectInt {
    #[new]
    pub fn new(left: i32, top: i32, right: i32, bottom: i32) -> Self {
        Self {
            left,
            top,
            right,
            bottom,
        }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct RectFloat {
    #[pyo3(get, set)]
    pub left: f32,
    #[pyo3(get, set)]
    pub top: f32,
    #[pyo3(get, set)]
    pub right: f32,
    #[pyo3(get, set)]
    pub bottom: f32,
}

#[pymethods]
impl RectFloat {
    #[new]
    pub fn new(left: f32, top: f32, right: f32, bottom: f32) -> Self {
        Self {
            left,
            top,
            right,
            bottom,
        }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct RectUInt {
    #[pyo3(get, set)]
    pub left: u32,
    #[pyo3(get, set)]
    pub top: u32,
    #[pyo3(get, set)]
    pub right: u32,
    #[pyo3(get, set)]
    pub bottom: u32,
}

#[pymethods]
impl RectUInt {
    #[new]
    pub fn new(left: u32, top: u32, right: u32, bottom: u32) -> Self {
        Self {
            left,
            top,
            right,
            bottom,
        }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct Matrix3x3 {
    #[pyo3(get, set)]
    pub x1: f32,
    #[pyo3(get, set)]
    pub y1: f32,
    #[pyo3(get, set)]
    pub z1: f32,
    #[pyo3(get, set)]
    pub x2: f32,
    #[pyo3(get, set)]
    pub y2: f32,
    #[pyo3(get, set)]
    pub z2: f32,
    #[pyo3(get, set)]
    pub x3: f32,
    #[pyo3(get, set)]
    pub y3: f32,
    #[pyo3(get, set)]
    pub z3: f32,
}

#[pymethods]
impl Matrix3x3 {
    #[new]
    pub fn new(data: [[f32; 3]; 3]) -> Self {
        Self {
            x1: data[0][0],
            y1: data[0][1],
            z1: data[0][2],
            x2: data[1][0],
            y2: data[1][1],
            z2: data[1][2],
            x3: data[2][0],
            y3: data[2][1],
            z3: data[2][2],
        }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct UUniqueID {
    pub inner: Uuid,
}

#[pymethods]
impl UUniqueID {
    #[new]
    pub fn new(bytes: [u8; 16]) -> Self {
        Self {
            inner: Uuid::from_bytes_le(bytes),
        }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct Color {
    #[pyo3(get, set)]
    pub r: u8,
    #[pyo3(get, set)]
    pub g: u8,
    #[pyo3(get, set)]
    pub b: u8,
    #[pyo3(get, set)]
    pub a: u8,
}

#[pymethods]
impl Color {
    #[new]
    pub fn new(r: u8, g: u8, b: u8, a: u8) -> Self {
        Color { r, g, b, a }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Serialize, Deserialize, Debug, Copy, Clone, Default)]
#[pyclass]
pub struct Vector3D {
    #[pyo3(get, set)]
    pub x: f32,
    #[pyo3(get, set)]
    pub y: f32,
    #[pyo3(get, set)]
    pub z: f32,
}

#[pymethods]
impl Vector3D {
    #[new]
    pub fn new(x: f32, y: f32, z: f32) -> Self {
        Self { x, y, z }
    }

    pub fn __str__(&self) -> String {
        format!("{:?}", self)
    }

    pub fn __repr__(&self) -> String {
        serde_json::to_string(self).unwrap()
    }
}

#[derive(Debug)]
pub struct LessThanThreeValues;

impl Display for LessThanThreeValues {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Less than three values were found within the vector.")
    }
}

impl std::error::Error for LessThanThreeValues {}

impl TryFrom<Vec<f32>> for Vector3D {
    type Error = LessThanThreeValues;

    fn try_from(value: Vec<f32>) -> Result<Self, Self::Error> {
        if value.len() < 3 {
            Err(LessThanThreeValues)
        } else {
            Ok(Vector3D {
                x: value[0],
                y: value[1],
                z: value[2],
            })
        }
    }
}

impl TryFrom<Vec<f64>> for Vector3D {
    type Error = LessThanThreeValues;

    fn try_from(value: Vec<f64>) -> Result<Self, Self::Error> {
        if value.len() < 3 {
            Err(LessThanThreeValues)
        } else {
            Ok(Vector3D {
                x: value[0] as f32,
                y: value[1] as f32,
                z: value[2] as f32,
            })
        }
    }
}

impl From<[f32; 3]> for Vector3D {
    fn from(value: [f32; 3]) -> Self {
        Self {
            x: value[0],
            y: value[1],
            z: value[2],
        }
    }
}

impl From<(f32, f32, f32)> for Vector3D {
    fn from(value: (f32, f32, f32)) -> Self {
        Self {
            x: value.0,
            y: value.1,
            z: value.2,
        }
    }
}
