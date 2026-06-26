#!/usr/bin/env bash
# Smoke test for the extension rename to Azclips of the Resources and Compute extensions.
# Exercises the renamed assemblies end-to-end against live Azure through the built CLI:
#   - Microsoft.Azure.Azclips.Extensions.Resources  (group list/show/create, rest GET/DELETE)
#   - Microsoft.Azure.Azclips.Extensions.Compute     (vm list)
#
# Prereqs:
#   - Build the solution first (dotnet build, which outputs to artifacts/Debug/...) so artifacts/Debug/Core.Cli/az(.exe) exists.
#   - A valid cached login for the test subscription.
#
# Usage: ./tests/Scripts/CLI/AzclipsRenameSmoke.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SUB="${SMOKE_SUB:?Set SMOKE_SUB to an Azure subscription id}"
RG="${SMOKE_RG:-azclips-smoke-rg}"
LOCATION="australiaeast"

# Clean up the negative-case scratch file on any exit.
tmpfile="$(mktemp)"
trap 'rm -f "$tmpfile"' EXIT

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

echo "=== [Resources] group list ==="
"$AZ" group list --subscription-id "$SUB"

echo "=== [Resources] ensure resource group exists ($RG) ==="
"$AZ" group create --subscription-id "$SUB" --name "$RG" --location "$LOCATION"

echo "=== [Resources] group show ($RG) ==="
"$AZ" group show --subscription-id "$SUB" --name "$RG"

echo "=== [Resources] rest GET ($RG) ==="
"$AZ" rest --method GET \
  --uri "/subscriptions/$SUB/resourcegroups/$RG?api-version=2021-04-01"

# Round-trip: create a throwaway group then delete it via rest DELETE
# (the Resources extension exposes create but not a dedicated delete verb).
TMP_RG="azclips-rename-smoke-$(date +%Y%m%d%H%M%S)"
echo "=== [Resources] group create ($TMP_RG) ==="
"$AZ" group create --subscription-id "$SUB" --name "$TMP_RG" --location "$LOCATION"

echo "=== [Resources] rest DELETE ($TMP_RG) ==="
"$AZ" rest --method DELETE \
  --uri "/subscriptions/$SUB/resourcegroups/$TMP_RG?api-version=2021-04-01"

echo "=== [Compute] vm list ($RG) ==="
"$AZ" vm list --subscription-id "$SUB" --resource-group "$RG"

echo "=== Negative case: group create with missing required --location should be rejected ==="
if "$AZ" group create --subscription-id "$SUB" --name "neg-$TMP_RG" >"$tmpfile" 2>&1; then
  echo "UNEXPECTED: command succeeded without --location"; cat "$tmpfile"; exit 1
else
  echo "OK: rejected as expected:"; cat "$tmpfile"
fi

echo "All Azclips rename smoke checks completed."
