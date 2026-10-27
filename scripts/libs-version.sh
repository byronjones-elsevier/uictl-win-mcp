#!/bin/sh
# List every NuGet library (direct and transitive) referenced by the source,
# with its resolved version. Usage: libs-version.sh [solution]
set -eu

TARGET="${1:-src/UICtl.Cli/UICtl.Cli.csproj}"
DOTNET="${DOTNET:-dotnet}"

"$DOTNET" list "$TARGET" package --include-transitive --no-restore 2>&1 |
awk '
	/error|warn/ && !/^ *>/ { print > "/dev/stderr"; next }
	/^ *> / { print $2, $NF }
' | sort -u | awk '{ n[NR] = $1; v[NR] = $2; if (length($1) > w) w = length($1) }
	END { for (i = 1; i <= NR; i++) printf "%-*s  %s\n", w, n[i], v[i] }'
