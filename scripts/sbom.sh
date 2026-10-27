#!/bin/sh
# Generate a CycloneDX JSON SBOM for the CLI project.
#
# Usage: sbom.sh <project> <output-dir>
#
# Uses the CycloneDX tool pinned in dotnet-tools.json
# (run `make sbom/install` once to create it).
set -eu

PROJECT="${1:?usage: $0 <project> <output-dir>}"
OUT_DIR="${2:?usage: $0 <project> <output-dir>}"
DOTNET="${DOTNET:-dotnet}"

if ! "$DOTNET" tool list 2>/dev/null | grep -qi '^cyclonedx'; then
	echo "error: CycloneDX tool not installed; run 'make sbom/install'" >&2
	exit 1
fi

mkdir -p "$OUT_DIR"
"$DOTNET" tool run dotnet-cyclonedx "$PROJECT" -F Json -o "$OUT_DIR" -fn bom.json
echo "wrote $OUT_DIR/bom.json"
