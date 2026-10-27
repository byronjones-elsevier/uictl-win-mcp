#!/bin/sh
# Generate a CycloneDX JSON SBOM for the CLI project.
#
# Usage: sbom.sh <project> <output-dir> [--skip-if-missing]
#
# With --skip-if-missing, a missing CycloneDX tool prints a warning and exits 0
# (used by publish so it is not blocked); real tool failures still fail.
#
# Uses the CycloneDX tool pinned in dotnet-tools.json
# (run `make sbom/install` once to create it).
set -eu

PROJECT="${1:?usage: $0 <project> <output-dir>}"
OUT_DIR="${2:?usage: $0 <project> <output-dir>}"
DOTNET="${DOTNET:-dotnet}"
SKIP_IF_MISSING="${3:-}"

if ! "$DOTNET" tool list 2>/dev/null | grep -qi '^cyclonedx'; then
	if [ "$SKIP_IF_MISSING" = "--skip-if-missing" ]; then
		echo "warning: CycloneDX tool not installed; skipping SBOM (run 'make sbom/install')" >&2
		exit 0
	fi
	echo "error: CycloneDX tool not installed; run 'make sbom/install'" >&2
	exit 1
fi

mkdir -p "$OUT_DIR"
"$DOTNET" tool run dotnet-CycloneDX "$PROJECT" -F Json -o "$OUT_DIR" -fn bom.json
echo "wrote $OUT_DIR/bom.json"
