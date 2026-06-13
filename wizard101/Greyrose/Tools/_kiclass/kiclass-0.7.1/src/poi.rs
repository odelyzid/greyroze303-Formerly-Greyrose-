use std::{
    collections::HashMap,
    fs,
    io::{self, Cursor, Read},
    path::Path,
};

use serde::{Deserialize, Serialize};

use crate::hashing::hash_string;

// ── Helpers ───────────────────────────────────────────────────────────────────

fn read_u8(r: &mut impl Read) -> io::Result<u8> {
    let mut b = [0u8; 1];
    r.read_exact(&mut b)?;
    Ok(b[0])
}

fn read_u16(r: &mut impl Read) -> io::Result<u16> {
    let mut b = [0u8; 2];
    r.read_exact(&mut b)?;
    Ok(u16::from_le_bytes(b))
}

fn read_u32(r: &mut impl Read) -> io::Result<u32> {
    let mut b = [0u8; 4];
    r.read_exact(&mut b)?;
    Ok(u32::from_le_bytes(b))
}

fn read_u64(r: &mut impl Read) -> io::Result<u64> {
    let mut b = [0u8; 8];
    r.read_exact(&mut b)?;
    Ok(u64::from_le_bytes(b))
}

fn read_f32(r: &mut impl Read) -> io::Result<f32> {
    let mut b = [0u8; 4];
    r.read_exact(&mut b)?;
    Ok(f32::from_le_bytes(b))
}

fn read_bool(r: &mut impl Read) -> io::Result<bool> {
    Ok(read_u8(r)? != 0)
}

fn read_str(r: &mut impl Read) -> io::Result<String> {
    let len = read_u32(r)? as usize;
    let mut buf = vec![0u8; len];
    r.read_exact(&mut buf)?;
    String::from_utf8(buf).map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))
}

// ── Structs ───────────────────────────────────────────────────────────────────

/// A single goal point (interactive event location).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiGoal {
    /// Unique goal ID (u64 key from the goals map).
    pub goal_id: u64,
    /// Whether the quest helper suppresses this point.
    pub no_quest_helper: bool,
    /// Index into the zone_names list.
    pub zone_id: u16,
    /// Resolved zone name for this goal's zone.
    pub zone_name: String,
    /// Template ID of the associated game object (0 if none).
    pub template_id: u64,
    /// World-space position [x, y, z].
    pub location: [f32; 3],
    /// Whether this point has an interactable NPC.
    pub interactable: bool,
    /// Whether this point has a collectable item.
    pub collectable: bool,
}

/// A zone and its list of interactive template IDs.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiInteractiveGoal {
    /// Zone identifier key (WizHash of zone path).
    pub zone_key: u32,
    /// Resolved zone path for this key (None if not found in zone_names).
    pub zone_object: Option<String>,
    /// Template IDs of interactable objects in this zone.
    pub template_ids: Vec<u64>,
}

/// A single zone-to-zone teleporter.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiTeleporter {
    /// Destination zone name.
    pub destination: String,
    /// World-space teleport position [x, y, z].
    pub position: [f32; 3],
}

/// A zone and its list of teleporters.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiZoneTeleporters {
    /// Zone identifier key (WizHash of zone path).
    pub zone_key: u32,
    /// Resolved zone path for this key (None if not found in zone_names).
    pub zone_object: Option<String>,
    /// Teleporters originating from this zone.
    pub teleporters: Vec<PoiTeleporter>,
}

/// A goal and its associated adjective IDs.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiGoalAdjective {
    /// Goal ID.
    pub goal_id: u64,
    /// List of adjective / modifier IDs for the goal.
    pub adjectives: Vec<u32>,
}

/// A single zone-mob entry (flat list: one NIF path per record).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiZoneMob {
    /// Zone identifier key (WizHash of zone path).
    pub zone_key: u32,
    /// Resolved zone path for this key (None if not found in zone_names).
    pub zone_object: Option<String>,
    /// Path to the mob's NIF model file.
    pub mob_nif: String,
}

/// Parsed representation of a `poi.dat` file.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PoiFile {
    /// All zone names referenced by this file (index = zone_id).
    pub zone_names: Vec<String>,
    /// Goal points: interactive event locations keyed by goal ID.
    pub goals: Vec<PoiGoal>,
    /// Per-zone lists of interactable template IDs.
    pub interactive_goals: Vec<PoiInteractiveGoal>,
    /// Per-zone lists of zone-to-zone teleporters.
    pub teleporters: Vec<PoiZoneTeleporters>,
    /// Per-goal adjective / modifier lists.
    pub goal_adjectives: Vec<PoiGoalAdjective>,
    /// Flat list of (zone_key, mob_nif) pairs describing zone mob populations.
    pub zone_mobs: Vec<PoiZoneMob>,
}

impl PoiFile {
    /// Parse a `poi.dat` file from disk.
    pub fn from_file<P: AsRef<Path>>(path: P) -> io::Result<Self> {
        let bytes = fs::read(path)?;
        Self::from_bytes(&bytes)
    }

    /// Parse a `poi.dat` file from raw bytes.
    pub fn from_bytes(data: &[u8]) -> io::Result<Self> {
        let mut r = Cursor::new(data);

        // Section 1 – zone name lookup table
        let zone_count = read_u32(&mut r)? as usize;
        let mut zone_names = Vec::with_capacity(zone_count);
        for _ in 0..zone_count {
            zone_names.push(read_str(&mut r)?);
        }

        // Build hash → zone_name reverse lookup from zone_names (section 1).
        // Will be extended with teleporter destinations after section 4 is parsed.
        let mut hash_to_zone: HashMap<u32, String> = zone_names
            .iter()
            .map(|name| (hash_string(name.as_bytes()), name.clone()))
            .collect();

        // Section 2 – goals: HashMap<u64, Point>
        let goals_count = read_u32(&mut r)? as usize;
        let mut goals = Vec::with_capacity(goals_count);
        for _ in 0..goals_count {
            let goal_id = read_u64(&mut r)?;
            let no_quest_helper = read_bool(&mut r)?;
            let zone_id = read_u16(&mut r)?;
            let template_id = read_u64(&mut r)?;
            let x = read_f32(&mut r)?;
            let y = read_f32(&mut r)?;
            let z = read_f32(&mut r)?;
            let interactable = read_bool(&mut r)?;
            let collectable = read_bool(&mut r)?;
            let zone_name = zone_names.get(zone_id as usize).cloned().unwrap_or_default();
            goals.push(PoiGoal {
                goal_id,
                no_quest_helper,
                zone_id,
                zone_name,
                template_id,
                location: [x, y, z],
                interactable,
                collectable,
            });
        }

        // Section 3 – interactive_goals: HashMap<u32, Vec<u64>>
        let ig_count = read_u32(&mut r)? as usize;
        let mut interactive_goals = Vec::with_capacity(ig_count);
        for _ in 0..ig_count {
            let zone_key = read_u32(&mut r)?;
            let inner_n = read_u32(&mut r)? as usize;
            let mut template_ids = Vec::with_capacity(inner_n);
            for _ in 0..inner_n {
                template_ids.push(read_u64(&mut r)?);
            }
            interactive_goals.push(PoiInteractiveGoal { zone_key, zone_object: None, template_ids });
        }

        // Section 4 – teleporters: HashMap<u32, Vec<Teleporter>>
        // Parse first without resolving zone_object — destinations are zone paths
        // that extend our hash lookup beyond what zone_names alone covers.
        let tp_count = read_u32(&mut r)? as usize;
        let mut teleporters = Vec::with_capacity(tp_count);
        for _ in 0..tp_count {
            let zone_key = read_u32(&mut r)?;
            let inner_n = read_u32(&mut r)? as usize;
            let mut tps = Vec::with_capacity(inner_n);
            for _ in 0..inner_n {
                let destination = read_str(&mut r)?;
                let px = read_f32(&mut r)?;
                let py = read_f32(&mut r)?;
                let pz = read_f32(&mut r)?;
                tps.push(PoiTeleporter { destination, position: [px, py, pz] });
            }
            teleporters.push(PoiZoneTeleporters { zone_key, zone_object: None, teleporters: tps });
        }

        // Extend the hash lookup with all teleporter destination strings.
        // These are zone paths that may not appear in zone_names.
        for tp_zone in &teleporters {
            for tp in &tp_zone.teleporters {
                let h = hash_string(tp.destination.as_bytes());
                hash_to_zone.entry(h).or_insert_with(|| tp.destination.clone());
            }
        }

        // Now resolve zone_object for interactive_goals and teleporters
        for ig in &mut interactive_goals {
            ig.zone_object = hash_to_zone.get(&ig.zone_key).cloned();
        }
        for tp_zone in &mut teleporters {
            tp_zone.zone_object = hash_to_zone.get(&tp_zone.zone_key).cloned();
        }

        // Section 5 – goal_adjectives: HashMap<u64, Vec<u32>>
        let ga_count = read_u32(&mut r)? as usize;
        let mut goal_adjectives = Vec::with_capacity(ga_count);
        for _ in 0..ga_count {
            let goal_id = read_u64(&mut r)?;
            let inner_n = read_u32(&mut r)? as usize;
            let mut adjectives = Vec::with_capacity(inner_n);
            for _ in 0..inner_n {
                adjectives.push(read_u32(&mut r)?);
            }
            goal_adjectives.push(PoiGoalAdjective { goal_id, adjectives });
        }

        // Section 6 – zone_mobs: flat list of (u32 zone_key, str mob_nif)
        let zm_count = read_u32(&mut r)? as usize;
        let mut zone_mobs = Vec::with_capacity(zm_count);
        for _ in 0..zm_count {
            let zone_key = read_u32(&mut r)?;
            let zone_object = hash_to_zone.get(&zone_key).cloned();
            let mob_nif = read_str(&mut r)?;
            zone_mobs.push(PoiZoneMob { zone_key, zone_object, mob_nif });
        }

        Ok(PoiFile { zone_names, goals, interactive_goals, teleporters, goal_adjectives, zone_mobs })
    }

    /// Serialize the parsed data to a pretty-printed JSON string.
    pub fn to_json(&self) -> String {
        serde_json::to_string_pretty(self).expect("PoiFile serialization failed")
    }
}
