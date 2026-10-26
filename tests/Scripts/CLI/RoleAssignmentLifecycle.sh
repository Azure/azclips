#!/usr/bin/env bash
# Role assignment lifecycle smoke test for the azclips CLI surface.
# Exercises create, list (by scope, by assignee, by role, inherited, all, and group-expanded),
# idempotent re-create (a generated name returns the existing assignment; an explicit different name
# surfaces the conflict), the --resource-group scope form with a fully-qualified role ID, an ABAC
# condition, the exact-scope default list, and delete (including ARM's dual 200 OK / 204 No Content
# response for deleting an already-deleted assignment, which must not be treated as an error). A fresh
# user-assigned identity is created to act as the principal, so the test is self-contained and needs no
# pre-existing fixture.
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

# Two more stable built-in roles for the extended-coverage checks below: "Monitoring Reader" is used
# (as a fully-qualified role definition ID) to exercise the --resource-group scope form with a
# non-Reader role, and "Storage Blob Data Reader" is a data-plane role whose blob read action the
# ABAC condition references, so ARM accepts a condition on it.
MONITORING_READER_ROLE_DEFINITION_ID='43d0d8ad-25c7-4714-9337-8ba259a9fe05'
STORAGE_BLOB_DATA_READER_ROLE_DEFINITION_ID='2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'

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

az_tsv() {
  local output
  output="$("$AZ" "$@" -o tsv)" || return $?
  printf '%s' "${output//$'\r'/}"
}

IDENTITY_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IDENTITY_NAME"
SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"
# Reused across every create attempt below (see the retry loop) so a retry after a transient
# client-side failure re-affirms the same PUT (idempotent for the same scope+name) and leaves the
# cleanup step a known name to delete. An omitted name would instead generate a new one each attempt;
# the idempotent conflict path would still recover the existing grant, but under a name this script
# never recorded.
ROLE_ASSIGNMENT_NAME="$(generate_guid)"
ROLE_ASSIGNMENT_DELETED=0
# Names of the extra grants created by the extended-coverage checks. Each is deleted inline right after
# it is tested; this array is a cleanup safety net in case the script exits in between (delete is 204-safe).
EXTRA_ASSIGNMENT_NAMES=()

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

  if [[ ${#EXTRA_ASSIGNMENT_NAMES[@]} -gt 0 ]]; then
    # Safety net for the extended-coverage grants; each is already deleted inline on the happy path, and
    # delete is idempotent (204 No Content), so re-deleting here is harmless. These are never intentionally
    # preserved by AZCLIPS_KEEP_ROLE_ASSIGNMENT; that switch only keeps the primary Reader grant.
    echo "=== [role assignment delete] cleanup extended-coverage grants (safety net) ==="
    for extra_name in "${EXTRA_ASSIGNMENT_NAMES[@]}"; do
      "$AZ" role assignment delete --scope "$SCOPE" --name "$extra_name" || true
    done
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

# identity show --ids returns a JSON array (it accepts multiple IDs), so index the first element.
PRINCIPAL_ID="$(az_tsv identity show --ids "$IDENTITY_ID" --query "[0].properties.principalId")"
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

echo "=== [role assignment create] idempotent re-create without --name returns the existing assignment ==="
# The same principal+role+scope already has an assignment (created above, under $ROLE_ASSIGNMENT_NAME).
# Re-running create WITHOUT --name generates a fresh name, so ARM returns a RoleAssignmentExists
# conflict; because the name was generated the idempotent path must look up and return the existing
# assignment instead of failing, and must not create a second grant.
RETURNED_NAME="$(az_tsv role assignment create \
  --scope "$SCOPE" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "$READER_ROLE_DEFINITION_ID" \
  --query name)"
if [[ "$RETURNED_NAME" != "$ROLE_ASSIGNMENT_NAME" ]]; then
  echo "Idempotent create returned '$RETURNED_NAME', expected the existing assignment '$ROLE_ASSIGNMENT_NAME'" >&2
  exit 1
fi
echo "Confirmed: generated-name re-create returned the existing assignment '$ROLE_ASSIGNMENT_NAME'."

echo "=== [role assignment create] explicit different --name for an existing grant surfaces the conflict ==="
# With an explicit --name the caller asked for a specific assignment, so a RoleAssignmentExists conflict
# must surface as an error rather than silently returning the differently-named existing assignment.
CONFLICT_NAME="$(generate_guid)"
set +e
CONFLICT_OUTPUT="$("$AZ" role assignment create \
  --scope "$SCOPE" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "$READER_ROLE_DEFINITION_ID" \
  --name "$CONFLICT_NAME" 2>&1)"
CONFLICT_EXIT=$?
set -e
if [[ "$CONFLICT_EXIT" -eq 0 ]]; then
  echo "Expected create with explicit --name '$CONFLICT_NAME' to fail with a conflict, but it succeeded" >&2
  # Clean up the unexpected extra assignment so it does not leak.
  "$AZ" role assignment delete --scope "$SCOPE" --name "$CONFLICT_NAME" || true
  exit 1
fi
if [[ "$CONFLICT_OUTPUT" != *"409"* || "$CONFLICT_OUTPUT" != *"RoleAssignmentExists"* ]]; then
  echo "Expected create with explicit --name '$CONFLICT_NAME' to fail with 409 RoleAssignmentExists, but got:" >&2
  echo "$CONFLICT_OUTPUT" >&2
  exit 1
fi
echo "Confirmed: explicit-name create surfaced the existing-assignment conflict."

echo "=== [role assignment list] default scope is exact and also appears in --include-inherited ==="
# The default list returns assignments at exactly this scope; --include-inherited additionally returns
# ancestor-scope (inherited) grants. Assert the default list contains only at-scope assignments and
# that every default assignment also appears in the inherited result set.
WRONG_SCOPE_COUNT=0
SCOPE_LOWER="${SCOPE,,}"
DEFAULT_SCOPES="$(az_tsv role assignment list --scope "$SCOPE" --query "[].properties.scope")"
while IFS= read -r default_scope; do
  [[ -z "$default_scope" ]] && continue
  if [[ "${default_scope,,}" != "$SCOPE_LOWER" ]]; then
    ((WRONG_SCOPE_COUNT++))
  fi
done <<< "$DEFAULT_SCOPES"
if [[ "$WRONG_SCOPE_COUNT" -ne 0 ]]; then
  echo "Default list included $WRONG_SCOPE_COUNT assignment(s) whose properties.scope was not '$SCOPE'" >&2
  exit 1
fi
DEFAULT_NAMES="$(az_tsv role assignment list --scope "$SCOPE" --query "[].name")"
INHERITED_NAMES="$(az_tsv role assignment list --scope "$SCOPE" --include-inherited --query "[].name")"
if ! grep -Fxq "$ROLE_ASSIGNMENT_NAME" <<< "$DEFAULT_NAMES"; then
  echo "Default exact-scope list did not contain the assignment '$ROLE_ASSIGNMENT_NAME' created at this scope" >&2
  exit 1
fi
while IFS= read -r default_name; do
  [[ -z "$default_name" ]] && continue
  if ! grep -Fxq "$default_name" <<< "$INHERITED_NAMES"; then
    echo "--include-inherited omitted default assignment '$default_name'" >&2
    exit 1
  fi
done <<< "$DEFAULT_NAMES"
echo "Confirmed: default list is exact-scope and every default assignment appears in --include-inherited."

echo "=== [role assignment create] --resource-group scope form with a fully-qualified role ID ==="
# Exercise the --resource-group scope form (vs an explicit --scope) together with a fully-qualified role
# definition ID (vs a bare GUID). Uses a different built-in role (Monitoring Reader) so this is a
# genuinely new grant rather than an idempotent conflict with the Reader grant above.
MONITORING_READER_FULL_ID="/subscriptions/$SUBSCRIPTION_ID/providers/Microsoft.Authorization/roleDefinitions/$MONITORING_READER_ROLE_DEFINITION_ID"
RG_FORM_NAME="$(generate_guid)"
EXTRA_ASSIGNMENT_NAMES+=("$RG_FORM_NAME")
"$AZ" role assignment create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "$MONITORING_READER_FULL_ID" \
  --name "$RG_FORM_NAME" \
  > /dev/null
echo "Created role assignment via --resource-group and a full role ID: $RG_FORM_NAME"

echo "=== [role assignment delete] --resource-group scope form ==="
"$AZ" role assignment delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$RG_FORM_NAME"
echo "Confirmed: created and deleted an assignment via the --resource-group scope form."

echo "=== [role assignment create] with an ABAC condition ==="
# Exercise the --condition / --condition-version path end-to-end: ARM validates the condition expression
# against the role's data actions, which the offline tests cannot. Uses Storage Blob Data Reader (whose
# blob read action the condition references) so ARM accepts the condition.
BLOB_CONDITION="((!(ActionMatches{'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read'})) OR (@Resource[Microsoft.Storage/storageAccounts/blobServices/containers:name] StringEquals 'smoke-test-container'))"
CONDITION_NAME="$(generate_guid)"
EXTRA_ASSIGNMENT_NAMES+=("$CONDITION_NAME")
"$AZ" role assignment create \
  --scope "$SCOPE" \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "$STORAGE_BLOB_DATA_READER_ROLE_DEFINITION_ID" \
  --condition "$BLOB_CONDITION" \
  --condition-version 2.0 \
  --name "$CONDITION_NAME" \
  > /dev/null
echo "Created conditional role assignment: $CONDITION_NAME"

PERSISTED_CONDITION="$(az_tsv role assignment list --scope "$SCOPE" --query "[?name=='$CONDITION_NAME'].properties | [0].condition")"
if [[ "$PERSISTED_CONDITION" != "$BLOB_CONDITION" ]]; then
  echo "Conditional assignment '$CONDITION_NAME' persisted condition '$PERSISTED_CONDITION', expected '$BLOB_CONDITION'" >&2
  exit 1
fi
PERSISTED_CONDITION_VERSION="$(az_tsv role assignment list --scope "$SCOPE" --query "[?name=='$CONDITION_NAME'].properties | [0].conditionVersion")"
if [[ "$PERSISTED_CONDITION_VERSION" != "2.0" ]]; then
  echo "Conditional assignment '$CONDITION_NAME' persisted condition version '$PERSISTED_CONDITION_VERSION', expected '2.0'" >&2
  exit 1
fi
echo "Confirmed: conditional assignment persisted the condition and condition version."
"$AZ" role assignment delete --scope "$SCOPE" --name "$CONDITION_NAME"
echo "Confirmed: created and deleted a conditional assignment."

if [[ "${AZCLIPS_KEEP_ROLE_ASSIGNMENT:-0}" == "1" ]]; then
  echo "AZCLIPS_KEEP_ROLE_ASSIGNMENT=1: skipping delete verification; role assignment left in place."
else
  echo "=== [role assignment delete] ==="
  "$AZ" role assignment delete --scope "$SCOPE" --name "$ROLE_ASSIGNMENT_NAME"
  ROLE_ASSIGNMENT_DELETED=1

  echo "=== [role assignment list] verify deleted assignment is gone ==="
  STILL_PRESENT="$(az_tsv role assignment list --scope "$SCOPE" --assignee-object-id "$PRINCIPAL_ID" \
    --query "length([?name=='$ROLE_ASSIGNMENT_NAME'])")"
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
