#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/wizard101/Greyrose"
OUTPUT_DIR="$SCRIPT_DIR/artifacts"

echo "=== Greyrose Multi-Platform Builder ==="
echo "Source: $PROJECT_DIR"
echo "Output: $OUTPUT_DIR"

# Build the Docker image (export stage — alpine with all artifacts)
echo ""
echo ">>> Building Docker image..."
docker build -t greyrose-builder --target export "$PROJECT_DIR"

# Extract artifacts using a temporary container
echo ""
echo ">>> Extracting artifacts..."
mkdir -p "$OUTPUT_DIR"

CONTAINER_ID=$(docker create greyrose-builder)

RIDS=("win-x64" "linux-x64" "linux-arm" "linux-arm64")
for rid in "${RIDS[@]}"; do
    rm -rf "$OUTPUT_DIR/$rid"
    mkdir -p "$OUTPUT_DIR/$rid"
    if docker cp "$CONTAINER_ID:/out/$rid/." "$OUTPUT_DIR/$rid/" 2>/dev/null; then
        echo "  ✓ $rid  ($(du -sh "$OUTPUT_DIR/$rid" | cut -f1))"
    else
        echo "  ✗ $rid  (not found)"
    fi
done

docker rm "$CONTAINER_ID" >/dev/null 2>&1

# Summary
echo ""
echo "=== Build Complete ==="
echo "Artifacts in: $OUTPUT_DIR"
for rid in "${RIDS[@]}"; do
    BIN=$(ls "$OUTPUT_DIR/$rid"/Greyrose* 2>/dev/null | head -1 || true)
    if [ -n "$BIN" ]; then
        SIZE=$(du -h "$BIN" 2>/dev/null | cut -f1)
        echo "  $rid:  $BIN  ($SIZE)"
    fi
done
