#!/usr/bin/env bash
# Role assignment lifecycle smoke test for the azclips CLI surface.
# Exercises create, list (by scope, by assignee, and by role), and delete (including ARM's dual
# 200 OK / 204 No Content response for deleting an already-deleted assignment, which must not be
# treated as an error). A fresh user-assigned identity is created to act as the principal, so the
# test is self-contained and needs no pre-existing fixture.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID            - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP       - resource group name; defaults to timestamped test group
#   AZCLIPS_IDENTITY_NAME        - principal identity name; defaults to timestamped test identity
#   AZCLIPS_LOCATION             - Azure region; defaults to westus2
#   AZCLIPS_KEEP_IDENTITY        - set to 1 to skip identity cleanup
#   AZCLIPS_KEEP_ROLE_ASSIGNMENT - set to 1 to skip delete verification and leave the role
#                                  assignment in place (e.g. to inspect it manually)
#   AZCLIPS_CLEANUP_RG           - set to 1 to delete the resource group at the end
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/RoleAssignmentLifecycle.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Git Bash/MSYS rewrites slash-prefixed arguments for native Windows exes
# (for example /subscriptions/... -> C:/Program Files/Git/subscriptions/...).
# ARM resource IDs and REST paths must be passed through unchanged.
export MSYS_NO_PATHCONV=1

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID:?AZCLIPS_SUBSCRIPTION_ID must be set.}"
TENANT_ID="${AZCLIPS_TENANT_ID:-}"
LOCATION="${AZCLIPS_LOCATION:-westus2}"
STAMP="$(date +%Y%m%d%H%M%S)"
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-roleassign-it-$STAMP}"
IDENTITY_NAME="${AZCLIPS_IDENTITY_NAME:-azclips-ra-id-$STAMP}"

# Built-in "Reader" role definition GUID (stable across all tenants); least-privilege choice for a
# smoke test that only needs to prove the grant works, not actually use it.
READER_ROLE_DEFINITION_ID='acdd72a7-3385-48ef-bd42-f606fba81ae7'

# Generates a GUID without relying on a specific toolchain: uuidgen isn't guaranteed present (e.g.
# some minimal Git Bash installs), so fall back to /dev/urandom, which is universally available on
# Linux, macOS, and Git Bash for Windows.
generate_guid() {
  if command -v uuidgen >/dev/null 2>&1; then
    uuidgen
  else
    local hex
    hex=$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')
    printf '%s-%s-%s-%s-%s\n' "${hex:0:8}" "${hex:8:4}" "${hex:12:4}" "${hex:16:4}" "${hex:20:12}"
  fi
}

IDENTITY_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IDENTITY_NAME"
SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"
# Reused across every create attempt below (see the retry loop) so a retry after a transient
# client-side failure safely re-affirms the same PUT instead of risking a RoleAssignmentExists
# conflict against an assignment that may have already succeeded server-side under a different name.
ROLE_ASSIGNMENT_NAME="$(generate_guid)"
ROLE_ASSIGNMENT_DELETED=0

cleanup() {
  local exit_code=$?

  if [[ "${AZCLIPS_KEEP_ROLE_ASSIGNMENT:-0}" != "1" && "$ROLE_ASSIGNMENT_DELETED" != "1" ]]; then
    # Safety net only: the main flow below already deletes (and verifies) the role assignment
    # when AZCLIPS_KEEP_ROLE_ASSIGNMENT isn't set. This only runs if something earlier in the
    # script failed before reaching that point. Delete is safe to call even if the assignment was
    # never created (204 No Content, not an error), so no existence check is needed here.
    echo "=== [role assignment delete] cleanup (safety net) ==="
    "$AZ" role assignment delete --scope "$SCOPE" --name "$ROLE_ASSIGNMENT_NAME" || true
  fi

  if [[ "${AZCLIPS_KEEP_IDENTITY:-0}" != "1" ]]; then
    echo "=== [identity delete] cleanup identity ==="
    "$AZ" identity delete --ids "$IDENTITY_ID" || true
  fi

  if [[ "${AZCLIPS_CLEANUP_RG:-0}" == "1" ]]; then
    echo "=== [group delete] cleanup resource group ==="
    "$AZ" rest \
      --method DELETE \
      --uri "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$RESOURCE_GROUP?api-version=2025-04-01" || true
  fi

  exit "$exit_code"
}
trap cleanup EXIT

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI. Disable the agent so failures surface as non-zero exits.
"$AZ" config set --key agent.enabled --value false

if [[ -n "$TENANT_ID" ]]; then
  echo "=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "=== [identity create] principal for the role assignment ==="
"$AZ" identity create \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --location "$LOCATION"

PRINCIPAL_ID="$("$AZ" identity show --ids "$IDENTITY_ID" --query properties.principalId -o tsv)"
if [[ -z "$PRINCIPAL_ID" ]]; then
  echo "identity show did not return a principal ID" >&2
  exit 1
fi

echo "=== [role assignment create] Reader for the identity (retrying for principal propagation) ==="
MAX_ATTEMPTS=6
for ((ATTEMPT = 1; ATTEMPT <= MAX_ATTEMPTS; ATTEMPT++)); do
  # A freshly-created principal can take a few seconds to propagate to ARM's authorization
  # backend; retry rather than fail outright on the first attempt.
  if "$AZ" role assignment create \
        --scope "$SCOPE" \
        --assignee-object-id "$PRINCIPAL_ID" \
        --assignee-principal-type ServicePrincipal \
        --role "$READER_ROLE_DEFINITION_ID" \
        --name "$ROLE_ASSIGNMENT_NAME" \
        --description "azclips role assignment lifecycle smoke test" \
        > /dev/null; then
    break
  fi
  if [[ "$ATTEMPT" -eq "$MAX_ATTEMPTS" ]]; then
    echo "role assignment create failed after $MAX_ATTEMPTS attempts" >&2
    exit 1
  fi
  echo "Attempt $ATTEMPT failed (principal may not have propagated yet). Retrying in 10s..."
  sleep 10
done
echo "Created role assignment: $ROLE_ASSIGNMENT_NAME"

echo "=== [role assignment list] by scope ==="
"$AZ" role assignment list --scope "$SCOPE"

echo "=== [role assignment list] filtered by assignee object ID ==="
"$AZ" role assignment list --scope "$SCOPE" --assignee-object-id "$PRINCIPAL_ID"

echo "=== [role assignment list] filtered by role definition ID ==="
"$AZ" role assignment list --scope "$SCOPE" --role "$READER_ROLE_DEFINITION_ID"

echo "=== [role assignment list] including inherited (ancestor-scope) assignments ==="
"$AZ" role assignment list --scope "$SCOPE" --include-inherited

echo "=== [role assignment list] across the whole subscription (every nested scope) ==="
"$AZ" role assignment list --all

echo "=== [role assignment list] expanding the principal's group memberships ==="
"$AZ" role assignment list --scope "$SCOPE" --assignee-object-id "$PRINCIPAL_ID" --include-groups

if [[ "${AZCLIPS_KEEP_ROLE_ASSIGNMENT:-0}" == "1" ]]; then
  echo "AZCLIPS_KEEP_ROLE_ASSIGNMENT=1: skipping delete verification; role assignment left in place."
else
  echo "=== [role assignment delete] ==="
  "$AZ" role assignment delete --scope "$SCOPE" --name "$ROLE_ASSIGNMENT_NAME"
  ROLE_ASSIGNMENT_DELETED=1

  echo "=== [role assignment list] verify deleted assignment is gone ==="
  STILL_PRESENT="$("$AZ" role assignment list --scope "$SCOPE" --assignee-object-id "$PRINCIPAL_ID" \
    --query "length([?name=='$ROLE_ASSIGNMENT_NAME'])" -o tsv)"
  if [[ "$STILL_PRESENT" != "0" ]]; then
    echo "Deleted role assignment '$ROLE_ASSIGNMENT_NAME' still appears in the list after delete" >&2
    exit 1
  fi
  echo "Confirmed: '$ROLE_ASSIGNMENT_NAME' no longer appears in the list."

  echo "=== [role assignment delete] again (ARM returns 204 No Content for an already-deleted assignment; must not be treated as an error) ==="
  "$AZ" role assignment delete --scope "$SCOPE" --name "$ROLE_ASSIGNMENT_NAME"
  echo "Confirmed: deleting an already-deleted assignment did not throw."
fi

echo "All role assignment lifecycle smoke checks completed."
