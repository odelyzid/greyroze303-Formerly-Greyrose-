# kiclass

`kiclass` is a Rust-powered Python extension for working with KingsIsle class definition files and binary XML data.

It can:

- load class tables from `.cdb` definition files,
- convert definition XML files into `.cdb`,
- merge client and server definition tables,
- deserialize KI binary XML files into dynamic Python objects,
- serialize parsed objects back to JSON or XML,
- expose the hashing helpers used by Wizard101 and Pirate101.
- parse `.nav` zone navigation map files.

## Install

From PyPI:

```bash
pip install kiclass
```

From source:

```bash
maturin develop --release
```

To build a wheel manually:

```bash
maturin build --release
```

## CDB Setup

`kiclass` loads class definitions from `.cdb` files. These are compiled class tables generated from KingsIsle definition XML files.

Typical layout:

```text
ClientDefs.xml
ServerDefs.xml
ClientDefs.cdb
ServerDefs.cdb
MergedDefs.cdb
```

Recommended workflow:

1. Obtain `ClientDefs.xml` and `ServerDefs.xml`.
2. Convert each XML file into a `.cdb` file.
3. Merge the client and server `.cdb` files into one combined table.
4. Load `MergedDefs.cdb` when reading binary XML files.

Example:

```python
from kiclass import Mode, convert_xml_to_cdb, merge_cdb_defs

convert_xml_to_cdb("ClientDefs.xml", "ClientDefs.cdb", Mode.Wizard)
convert_xml_to_cdb("ServerDefs.xml", "ServerDefs.cdb", Mode.Wizard)
merge_cdb_defs("ClientDefs.cdb", "ServerDefs.cdb", "MergedDefs.cdb")
```

If you are working with Pirate101 data, use `Mode.Pirate` when generating the `.cdb` files.

`kiclass` expects the current `.cdb` file format. If your generated files are stale or invalid, regenerate them from the XML sources instead of relying on backwards compatibility.

## Usage

### Load a class table and read a binary XML file

```python
from kiclass import KIBinaryFileReader, load_classes

classes = load_classes("MergedDefs.cdb")
reader = KIBinaryFileReader(classes)

result = reader.read_file("some_file.xml")

if result is None:
	print("Not a valid KI binary XML file")
elif not result.is_known():
	print(f"Unknown class id: {result.id()}")
else:
	print(result.class_name())
	print(result.to_json())
```

### Create a class from the loaded table

```python
from kiclass import load_classes

classes = load_classes("MergedDefs.cdb")
obj = classes.create_class("ClientObject")

if obj is not None:
	print(obj.class_name())
	print(obj.to_json())
```

### Use the hashing helpers

```python
from kiclass import Mode, hash_field_type, light_hash_string, wiz_hash_string

print(wiz_hash_string("ClientObject"))
print(light_hash_string("string"))
print(hash_field_type("m_templateID", "unsigned int", Mode.Wizard))
```

## Notes

- `read_file()` returns a dynamic class for known data, `UnknownClass` for valid but unmapped data, and `None` for invalid binary XML.
- The loader expects `.cdb` files generated in the current format.
- The repository includes scripts such as `convert_worlddata.py` and the `easy_deserialize` tooling for larger batch workflows.

## Nav files

`.nav` files are zlib-compressed zone navigation maps.  Each node represents a
game zone and each edge a navigable connection (e.g. ship route, storm gate)
between zones on the world map.

```python
from kiclass import parse_nav

nav = parse_nav("zonemap.nav")

print(nav)                    # NavMap(nodes=3352, edges=10193)
print(nav.node_count)         # 3352
print(nav.edge_count)         # 10193

# Look up a zone by name
node = nav.node_by_zone("Aquila/AQ_Z00_Hub")
print(node.index, node.zone_name)

# Find all zones reachable from node 0
for nid in nav.neighbors(0):
    print(nav.get_zone_name(nid))

# Iterate all nodes / edges
for node in nav.nodes:
    print(node.index, node.zone_name)

for edge in nav.edges:
    print(edge.from_node, "->", edge.to_node)
```
