use std::{
    fs,
    io::{self, Cursor, Read},
    path::Path,
};

use flate2::read::ZlibDecoder;
use serde::{Deserialize, Serialize};

// ── Format A: Zone Map (e.g. zonemap.nav) ────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ZoneMapNode {
    pub index: u32,
    pub zone_name_idx: u32,
    pub zone_name: String,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct ZoneMapEdge {
    pub from_node: u16,
    pub to_node: u16,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ZoneMap {
    pub nodes: Vec<ZoneMapNode>,
    pub edges: Vec<ZoneMapEdge>,
}

// ── Format B: Zone Nav mesh (e.g. zone.nav) ──────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ZoneNavNode {
    pub id: u16,
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct ZoneNavEdge {
    pub from_node: u16,
    pub to_node: u16,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ZoneNavMap {
    pub nodes: Vec<ZoneNavNode>,
    pub edges: Vec<ZoneNavEdge>,
}

// ── NavFile enum ─────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "format", rename_all = "snake_case")]
pub enum NavFile {
    ZoneMap(ZoneMap),
    ZoneNav(ZoneNavMap),
}

impl NavFile {
    pub fn from_file(path: impl AsRef<Path>) -> io::Result<Self> {
        let compressed = fs::read(path)?;
        Self::from_bytes(&compressed)
    }

    pub fn from_bytes(data: &[u8]) -> io::Result<Self> {
        let mut decoder = ZlibDecoder::new(data);
        let mut decompressed = Vec::new();
        decoder.read_to_end(&mut decompressed)?;

        // Empty zone nav (all-zero or too short) → empty ZoneNav
        if decompressed.len() < 8 || decompressed.iter().all(|&b| b == 0) {
            return Ok(NavFile::ZoneNav(ZoneNavMap { nodes: vec![], edges: vec![] }));
        }

        let h0 = u16::from_le_bytes([decompressed[0], decompressed[1]]);
        let h1 = u16::from_le_bytes([decompressed[2], decompressed[3]]);

        if h0 == h1 {
            parse_zone_map(&decompressed).map(NavFile::ZoneMap)
        } else {
            parse_zone_nav(&decompressed).map(NavFile::ZoneNav)
        }
    }

    pub fn to_json(&self) -> serde_json::Result<String> {
        serde_json::to_string_pretty(self)
    }
}

// ── Format A parser ──────────────────────────────────────────────────────────

fn parse_zone_map(decompressed: &[u8]) -> io::Result<ZoneMap> {
    let mut cur = Cursor::new(decompressed);

    // Header: u16 node_count, u16 node_count (same value twice)
    let node_count = read_u16(&mut cur)? as usize;
    let _ = read_u16(&mut cur)?;

    // Nodes: node_count × 14 bytes — u32 zone_name_idx | [u8; 10] reserved
    let mut nodes: Vec<ZoneMapNode> = Vec::with_capacity(node_count);
    for i in 0..node_count {
        let zone_name_idx = read_u32(&mut cur)?;
        let mut _reserved = [0u8; 10];
        cur.read_exact(&mut _reserved)?;
        nodes.push(ZoneMapNode {
            index: i as u32,
            zone_name_idx,
            zone_name: String::new(),
        });
    }

    // Edge section: u16 max_node_idx, u16 edge_count, then edges (u16 to_node, u16 from_node)
    let _ = read_u16(&mut cur)?;
    let edge_count = read_u16(&mut cur)? as usize;
    let mut edges: Vec<ZoneMapEdge> = Vec::with_capacity(edge_count);
    for _ in 0..edge_count {
        let to_node = read_u16(&mut cur)?;
        let from_node = read_u16(&mut cur)?;
        edges.push(ZoneMapEdge { from_node, to_node });
    }

    // Zone-string section: 6-byte header (u16,u16,u16) then node_count × (u32 len, utf-8)
    let _ = read_u16(&mut cur)?;
    let _ = read_u16(&mut cur)?;
    let _ = read_u16(&mut cur)?;
    for node in &mut nodes {
        let len = read_u32(&mut cur)? as usize;
        let mut buf = vec![0u8; len];
        cur.read_exact(&mut buf)?;
        node.zone_name = String::from_utf8_lossy(&buf).into_owned();
    }

    Ok(ZoneMap { nodes, edges })
}

// ── Format B parser ──────────────────────────────────────────────────────────

fn parse_zone_nav(decompressed: &[u8]) -> io::Result<ZoneNavMap> {
    let mut cur = Cursor::new(decompressed);

    // Header: u16 max_node_id (skip), u32 node_count
    let _ = read_u16(&mut cur)?;
    let node_count = read_u32(&mut cur)? as usize;

    // Nodes: node_count × 14 bytes — f32 x, f32 y, f32 z, u16 id
    let mut nodes: Vec<ZoneNavNode> = Vec::with_capacity(node_count);
    for _ in 0..node_count {
        let x = read_f32(&mut cur)?;
        let y = read_f32(&mut cur)?;
        let z = read_f32(&mut cur)?;
        let id = read_u16(&mut cur)?;
        nodes.push(ZoneNavNode { id, x, y, z });
    }

    // Edge section: u32 edge_count, then edges (u16 from_node, u16 to_node)
    let edge_count = read_u32(&mut cur)? as usize;
    let mut edges: Vec<ZoneNavEdge> = Vec::with_capacity(edge_count);
    for _ in 0..edge_count {
        let from_node = read_u16(&mut cur)?;
        let to_node = read_u16(&mut cur)?;
        edges.push(ZoneNavEdge { from_node, to_node });
    }

    Ok(ZoneNavMap { nodes, edges })
}

// ── Helpers ──────────────────────────────────────────────────────────────────

fn read_u16(cur: &mut Cursor<&[u8]>) -> io::Result<u16> {
    let mut buf = [0u8; 2];
    cur.read_exact(&mut buf)?;
    Ok(u16::from_le_bytes(buf))
}

fn read_u32(cur: &mut Cursor<&[u8]>) -> io::Result<u32> {
    let mut buf = [0u8; 4];
    cur.read_exact(&mut buf)?;
    Ok(u32::from_le_bytes(buf))
}

fn read_f32(cur: &mut Cursor<&[u8]>) -> io::Result<f32> {
    let mut buf = [0u8; 4];
    cur.read_exact(&mut buf)?;
    Ok(f32::from_le_bytes(buf))
}
