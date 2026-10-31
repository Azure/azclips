#!/usr/bin/env bash
# Federated identity credential lifecycle smoke test for the azclips CLI surface.
# Exercises create, list, show, update (including generic set/add/remove), and delete of
# federated identity credentials under a user assigned identity. Note: `--ids` is not supported
# for federated-credential sub-commands (matching az-cli parity), so named arguments are used
# throughout; `--ids` is only used for the plain `identity delete` cleanup step.
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
#   AZCLIPS_IDENTITY_NAME   - identity name; defaults to timestamped test identity
#   AZCLIPS_LOCATION        - Azure region; defaults to westus2
#   AZCLIPS_KEEP_IDENTITY   - set to 1 to skip identity/credential cleanup
#   AZCLIPS_CLEANUP_RG      - set to 1 to delete the resource group at the end
#
# Usage:
#   AZCLIPS_TENANT_ID=<tenant> AZCLIPS_SUBSCRIPTION_ID=<subscription> ./tests/Scripts/CLI/FederatedIdentityCredentialLifecycle.sh
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
RESOURCE_GROUP="${AZCLIPS_RESOURCE_GROUP:-azclips-fic-it-$STAMP}"
IDENTITY_NAME="${AZCLIPS_IDENTITY_NAME:-azclips-id-$STAMP}"
SUBJECT_FIC_NAME="azclips-fic-subject-$STAMP"
EXPRESSION_FIC_NAME="azclips-fic-expression-$STAMP"

IDENTITY_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$IDENTITY_NAME"

cleanup() {
  local exit_code=$?

  if [[ "${AZCLIPS_KEEP_IDENTITY:-0}" != "1" ]]; then
    echo "=== [identity federated-credential delete] cleanup ==="
    for fic_name in "$SUBJECT_FIC_NAME" "$EXPRESSION_FIC_NAME"; do
      "$AZ" identity federated-credential delete \
        --subscription "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --identity-name "$IDENTITY_NAME" \
        --name "$fic_name" || true
    done

    echo "=== [identity delete] cleanup ==="
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
"$AZ" config set --key agent.enabled --value false --force

if [[ -n "$TENANT_ID" ]]; then
  echo "=== [login] ==="
  "$AZ" login --tenant-id "$TENANT_ID" --subscription-id "$SUBSCRIPTION_ID"
fi

echo "=== [group create] $RESOURCE_GROUP ==="
"$AZ" group create \
  --subscription-id "$SUBSCRIPTION_ID" \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

echo "=== [identity create] ==="
"$AZ" identity create \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --location "$LOCATION"

echo "=== [identity federated-credential create] using subject ==="
"$AZ" identity federated-credential create \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$SUBJECT_FIC_NAME" \
  --issuer "https://kubernetes-oauth.azure.com" \
  --subject "system:serviceaccount:ns:svcaccount" \
  --audiences api://AzureADTokenExchange

echo "=== [identity federated-credential create] using claims matching expression ==="
"$AZ" identity federated-credential create \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$EXPRESSION_FIC_NAME" \
  --issuer "https://token.actions.githubusercontent.com" \
  --claims-matching-expression-version 1 \
  --claims-matching-expression-value "claims['sub'] matches 'repo:contoso/example:ref:refs/heads/*' and claims['repository_id'] eq '456789'" \
  --audiences api://AzureADTokenExchange

echo "=== [identity federated-credential list] ==="
"$AZ" identity federated-credential list \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME"

echo "=== [identity federated-credential show] subject credential ==="
"$AZ" identity federated-credential show \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$SUBJECT_FIC_NAME"

echo "=== [identity federated-credential show] expression credential ==="
"$AZ" identity federated-credential show \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$EXPRESSION_FIC_NAME"

echo "=== [identity federated-credential update] subject audience override ==="
"$AZ" identity federated-credential update \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$SUBJECT_FIC_NAME" \
  --audiences api://AzureADTokenExchange-Modified

echo "=== [identity federated-credential update] expression audience override ==="
"$AZ" identity federated-credential update \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$EXPRESSION_FIC_NAME" \
  --audiences api://AzureADTokenExchange-Modified

echo "=== [identity federated-credential update] subject generic set ==="
"$AZ" identity federated-credential update \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$SUBJECT_FIC_NAME" \
  --set properties.issuer=https://kubernetes-oauth-2.azure.com

echo "=== [identity federated-credential show] subject after update ==="
"$AZ" identity federated-credential show \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$SUBJECT_FIC_NAME"

echo "=== [identity federated-credential show] expression after update ==="
"$AZ" identity federated-credential show \
  --subscription "$SUBSCRIPTION_ID" \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name "$EXPRESSION_FIC_NAME"

echo "All federated identity credential lifecycle smoke checks completed."
