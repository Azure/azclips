#!/usr/bin/env bash
# Managed identity lifecycle smoke test for the azclips CLI surface.
# Exercises create, list, show, update, generic update, resource restrictions, and delete.
#
# Prereqs:
#   1. Build the solution: dotnet build azclips.slnx
#   2. Sign in, or provide AZCLIPS_TENANT_ID so this script can sign in.
#
# Required:
#   AZCLIPS_SUBSCRIPTION_ID - subscription used for the live test resources
#
# Optional:
#   AZCLIPS_TENANT_ID       - tenant to sign in with before running commands
#   AZCLIPS_RESOURCE_GROUP  - resource group name; defaults to timestamped test group
#   AZCLIPS_IDENTITY_NAME   - primary identity name; defaults to timestamped test identity
#   AZCLIPS_LOCATION        - Azure region; defaults to westus2
#   AZCLIPS_KEEP_IDENTITY   - set to 1 to skip identity cleanup
#   AZCLIPS_CLEANUP_RG      - set to 1 to delete the resource group at the end
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/ManagedServiceIdentityLifecycle.sh
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
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-identity-it-$STAMP}"
IDENTITY_NAME="${AZCLIPS_IDENTITY_NAME:-azclips-id-$STAMP}"
SECONDARY_IDENTITY_NAME="$IDENTITY_NAME-2"

IDENTITY_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IDENTITY_NAME"
SECONDARY_IDENTITY_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$SECONDARY_IDENTITY_NAME"

cleanup() {
  local exit_code=$?

  if [[ "${AZCLIPS_KEEP_IDENTITY:-0}" != "1" ]]; then
    echo "=== [identity delete] cleanup both identities ==="
    "$AZ" identity delete --ids "$IDENTITY_ID" "$SECONDARY_IDENTITY_ID" || true
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
"$AZ" config set --key agent.recovery.trigger --value off

if [[ -n "$TENANT_ID" ]]; then
  echo "=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "=== [identity create] primary ==="
"$AZ" identity create \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --location "$LOCATION" \
  --tags env=local-integration \
  --isolation-scope Regional \
  --resource-restriction '{"providers":["Microsoft.Compute"]}'

echo "=== [identity create] secondary ==="
"$AZ" identity create \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SECONDARY_IDENTITY_NAME" \
  --location "$LOCATION" \
  --tags env=local-integration \
  --isolation-scope Regional \
  --resource-restriction '{"providers":["Microsoft.Compute"]}'

echo "=== [identity list] resource group ==="
"$AZ" identity list \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP"

echo "=== [identity show] multiple ids ==="
"$AZ" identity show --ids "$IDENTITY_ID" "$SECONDARY_IDENTITY_ID"

echo "=== [identity update] normal path without ids ==="
"$AZ" identity update \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --isolation-scope None \
  --tags env=normal-update

echo "=== [identity update] single id ==="
"$AZ" identity update \
  --ids "$SECONDARY_IDENTITY_ID" \
  --isolation-scope None \
  --tags env=single-id-update

echo "=== [identity update] resource restriction provider list ==="
"$AZ" identity update \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --resource-restriction '{"providers":["Microsoft.Storage/accounts"]}'

echo "=== [identity update] generic set/add/remove ==="
"$AZ" identity update \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --set properties.isolationScope=Regional \
  --add properties.assignmentRestrictions.providers Microsoft.KeyVault \
  --remove properties.assignmentRestrictions.providers

echo "=== [identity update] multiple ids ==="
"$AZ" identity update \
  --ids "$IDENTITY_ID" "$SECONDARY_IDENTITY_ID" \
  --isolation-scope None \
  --tags env=local-integration-updated

echo "=== [identity show] multiple ids after update ==="
"$AZ" identity show --ids "$IDENTITY_ID" "$SECONDARY_IDENTITY_ID"

echo "All identity lifecycle smoke checks completed."
