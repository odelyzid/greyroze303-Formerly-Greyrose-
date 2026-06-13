#!/usr/bin/env python3
"""
Convert all .vdf and .aad files in a given directory from binary XML to JSON.

Place ClientDefs.xml and ServerDefs.xml in the same directory as this script.
Usage: python convert_worlddata.py /path/to/Mob-WorldData
"""

import os
import traceback
import sys
from pathlib import Path

# Third‑party library (must be installed / accessible)
from kiclass import (
    KIBinaryFileReader,
    load_classes,
    convert_xml_to_cdb,
    merge_cdb_defs,
    Mode
)


def prepare_class_table(script_dir: Path):
    """Ensure CDB files exist and return a loaded DynamicClassTable."""
    client_xml = script_dir / "PirateClientDefs.xml"
    server_xml = script_dir / "PirateServerDefs.xml"
    client_cdb = script_dir / "PirateClientDefs.cdb"
    server_cdb = script_dir / "PirateServerDefs.cdb"
    merged_cdb = script_dir / "PirateMergedDefs.cdb"

    if not client_xml.exists():
        raise FileNotFoundError(f"Missing {client_xml}")
    if not server_xml.exists():
        raise FileNotFoundError(f"Missing {server_xml}")

    # Convert XML → CDB if needed
    #if not client_cdb.exists():
    print("Creating ClientDefs.cdb ...")
    try:
        if client_cdb.exists():
            os.remove(str(client_cdb))
        convert_xml_to_cdb(str(client_xml), str(client_cdb), Mode.Pirate)
        test = load_classes(str(client_cdb))
        print("Client Classes were successful")
    except Exception as e:
        raise e
    #if not server_cdb.exists():
    print("Creating ServerDefs.cdb ...")
    try:
        if server_cdb.exists():
            os.remove(str(server_cdb))
        convert_xml_to_cdb(str(server_xml), str(server_cdb), Mode.Pirate)
        test = load_classes(str(server_cdb))
        print("Server Classes were successful")
    except Exception as e:
        raise e

    # Merge definitions (client priority)
    if not merged_cdb.exists():
        print("Creating MergedDefs.cdb ...")
        merge_cdb_defs(str(client_cdb), str(server_cdb), str(merged_cdb), Mode.Pirate)

    print("Loading class definitions ...")
    return load_classes(str(merged_cdb))


def process_file(reader: KIBinaryFileReader, file_path: Path) -> bool:
    """Deserialize binary XML and overwrite with JSON. Returns True on success."""
    try:
        # Read binary XML
        result = reader.read_file(str(file_path))

        if result is None:
            print(f"  Skipped (not valid binary XML): {file_path}")
            return False

        if not result.is_known():
            # UnknownClass – we cannot convert to JSON
            print(f"  Skipped (unknown class, id={result.id()}): {file_path}")
            return False

        # DynamicClass → JSON string
        json_str = result.to_json()

        # Overwrite the file with JSON text
        output_dir = Path.cwd() / "output"
        output_dir.mkdir(parents=True, exist_ok=True)
        
        output_file = output_dir / file_path.with_suffix('.json').name
        output_file.write_text(json_str, encoding="utf-8")
        print(f"  Converted: {file_path}")
        return True

    except Exception as e:
        print(f"  Error processing {file_path}: {e}")
        return False


def main():
    if len(sys.argv) != 2:
        print("Usage: python convert_worlddata.py <Mob-WorldData directory>")
        sys.exit(1)

    target_dir = Path(sys.argv[1]).resolve()
    if not target_dir.is_dir():
        print(f"Error: '{target_dir}' is not a valid directory.")
        sys.exit(1)

    script_dir = Path(__file__).parent.resolve()

    # Prepare class definitions
    try:
        class_table = prepare_class_table(script_dir)
    except Exception as e:
        print(f"Failed to load class definitions: {traceback.format_exc()}")
        sys.exit(1)

    reader = KIBinaryFileReader(class_table)

    # Find all .vdf and .aad files
    extensions = {".vdf", ".aad", ".xml"}
    files = [p for p in target_dir.rglob("*") if p.suffix.lower() in extensions]

    if not files:
        print(f"No .vdf, .xml, or .aad files found in {target_dir}")
        return

    print(f"Found {len(files)} file(s) to process.\n")

    success = 0
    for file_path in files:
        if process_file(reader, file_path):
            success += 1

    print(f"\nDone. Converted {success}/{len(files)} files.")


if __name__ == "__main__":
    main()