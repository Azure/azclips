#!/usr/bin/env bash
# Smoke test for the msgraph group commands (show, list, member list, member check).
# Exercises both the GUID path and the display-name-prefix resolver, plus the two documented
# error cases (ambiguous prefix, no match).
#
# Prereqs:
#   1. Build the solution:            dotnet build azclips.slnx
#   2. Install/refresh the extension: az extension add --path artifacts/Debug/Microsoft.Azure.Azclips.Extensions.MsGraph/Microsoft.Azure.Azclips.Extensions.MsGraph.0.1.0.nupkg
#   3. Sign in:                       az login --tenant-id <tenant>
#
# Override the test-tenant fixtures via env vars if running against a different tenant:
#   AZCLIPS_MSGRAPH_GROUP_ID           - a group's object ID
#   AZCLIPS_MSGRAPH_GROUP_NAME         - the same group's exact display name
#   AZCLIPS_MSGRAPH_GROUP_PREFIX       - a name prefix that matches exactly one group
#   AZCLIPS_MSGRAPH_GROUP_PREFIX_MANY  - a name prefix that matches >1 groups
#   AZCLIPS_MSGRAPH_MEMBER_ID          - object ID of a user known not to be in the group above
#
# Usage: ./tests/Scripts/CLI/MsGraphGroup.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Defaults match the shared test tenant (azclitools20260430.onmicrosoft.com).
GROUP_ID="${AZCLIPS_MSGRAPH_GROUP_ID:-e311fa4a-a740-4a3b-af0d-e538e2b01926}"
GROUP_NAME="${AZCLIPS_MSGRAPH_GROUP_NAME:-GroupA}"
GROUP_PREFIX="${AZCLIPS_MSGRAPH_GROUP_PREFIX:-GroupA}"
GROUP_PREFIX_MANY="${AZCLIPS_MSGRAPH_GROUP_PREFIX_MANY:-Group}"
MEMBER_ID="${AZCLIPS_MSGRAPH_MEMBER_ID:-4c7fe051-c052-4557-94e8-a13ed4d5fd38}"

echo "=== [group show] by object ID ==="
"$AZ" msgraph group show --group "$GROUP_ID"

echo "=== [group show] by display name (prefix resolver) ==="
"$AZ" msgraph group show --group "$GROUP_PREFIX"

echo "=== [group show] -g short alias ==="
"$AZ" msgraph group show -g "$GROUP_ID"

echo "=== [group list] all groups (first page) ==="
"$AZ" msgraph group list

echo "=== [group list] --display-name prefix ==="
"$AZ" msgraph group list --display-name "$GROUP_PREFIX_MANY"

echo "=== [group list] --filter OData ==="
"$AZ" msgraph group list --filter "securityEnabled eq true"

echo "=== [group member list] by object ID ==="
"$AZ" msgraph group member list --group "$GROUP_ID"

echo "=== [group member list] by display name ==="
"$AZ" msgraph group member list --group "$GROUP_NAME"

echo "=== [group member check] by object ID ==="
"$AZ" msgraph group member check --group "$GROUP_ID" --member-id "$MEMBER_ID"

echo "=== [group member check] by display name ==="
"$AZ" msgraph group member check --group "$GROUP_NAME" --member-id "$MEMBER_ID"

echo "=== Negative: ambiguous prefix should error with 'more than one group matched' ==="
if "$AZ" msgraph group show --group "$GROUP_PREFIX_MANY" >/tmp/msgraph_ambiguous.out 2>&1; then
  echo "UNEXPECTED: command succeeded on ambiguous prefix"; cat /tmp/msgraph_ambiguous.out; exit 1
else
  echo "OK: rejected as expected:"; cat /tmp/msgraph_ambiguous.out
fi

echo "=== Negative: name with no matches should error with 'is not found in Graph' ==="
if "$AZ" msgraph group show --group "no-such-group-$(date +%s)" >/tmp/msgraph_nomatch.out 2>&1; then
  echo "UNEXPECTED: command succeeded on non-existent name"; cat /tmp/msgraph_nomatch.out; exit 1
else
  echo "OK: rejected as expected:"; cat /tmp/msgraph_nomatch.out
fi

echo "All msgraph group smoke checks completed."
