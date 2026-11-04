#!/usr/bin/env bash
# one-time (re-runnable) bootstrap for bug-bash-identity-<alias>. creates the resource
# group, managed identity, federated credential, and matching GitHub environment,
# using the classic az/gh CLIs since there's no credential yet before the trust exists.
#
# pair of: tests/Live.Tests/Identity/bug-bash-identity-cleanup.sh
# usage: ./tests/Live.Tests/Identity/bug-bash-identity-bootstrap.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=bug-bash-identity-env.sh
source "$SCRIPT_DIR/bug-bash-identity-env.sh"

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
LOCATION="australiaeast"
RESOURCE_GROUP="bug-bash-identity-$ALIAS"
IDENTITY_NAME="managed-identity-$ALIAS"
ENVIRONMENT="bug-bash-identity"
FEDERATED_CRED_NAME="mi-federated-credential-$ALIAS"

# "Use immutable subject claim" (Settings > Actions > General > OIDC configuration) changes
# the OIDC token's `sub` from plain owner/repo to owner@ownerId/repo@repoId. Query GitHub for
# the actual configured prefix instead of assuming either format, so this works whether or
# not that setting is on, and keeps working if it's toggled later.
read -r USE_DEFAULT SUB_CLAIM_PREFIX <<< "$(gh api "repos/$REPO/actions/oidc/customization/sub" --jq '[(.use_default|tostring), (.sub_claim_prefix // "")] | @tsv')"
if [[ "$USE_DEFAULT" == "true" && -n "$SUB_CLAIM_PREFIX" ]]; then
  # sub_claim_prefix already includes the leading "repo:" (e.g. "repo:owner@id/repo@id").
  FEDERATED_SUBJECT="${SUB_CLAIM_PREFIX}:environment:${ENVIRONMENT}"
else
  FEDERATED_SUBJECT="repo:${REPO}:environment:${ENVIRONMENT}"
fi

az account set --subscription "$SUBSCRIPTION_ID"

echo "Creating resource group '$RESOURCE_GROUP'..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

echo "Creating managed identity '$IDENTITY_NAME'..."
az identity create --name "$IDENTITY_NAME" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" -o none

CLIENT_ID="$(az identity show --name "$IDENTITY_NAME" --resource-group "$RESOURCE_GROUP" --query clientId -o tsv)"
PRINCIPAL_ID="$(az identity show --name "$IDENTITY_NAME" --resource-group "$RESOURCE_GROUP" --query principalId -o tsv)"
TENANT_ID="$(az identity show --name "$IDENTITY_NAME" --resource-group "$RESOURCE_GROUP" --query tenantId -o tsv)"
RG_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

echo "Assigning Contributor on '$RESOURCE_GROUP' to the identity..."
az role assignment create \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role Contributor \
  --scope "$RG_ID" -o none

echo "Creating federated credential '$FEDERATED_CRED_NAME' (subject: $FEDERATED_SUBJECT)..."
az identity federated-credential create \
  --name "$FEDERATED_CRED_NAME" \
  --identity-name "$IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "$FEDERATED_SUBJECT" \
  --audiences "api://AzureADTokenExchange" -o none

echo "Creating/updating GitHub environment '$ENVIRONMENT' on $REPO..."
gh api -X PUT "repos/$REPO/environments/$ENVIRONMENT" --silent

echo "Setting GitHub environment variables..."
gh variable set AZURE_CLIENT_ID --env "$ENVIRONMENT" --body "$CLIENT_ID" --repo "$REPO"
gh variable set AZURE_TENANT_ID --env "$ENVIRONMENT" --body "$TENANT_ID" --repo "$REPO"
gh variable set AZURE_SUBSCRIPTION_ID --env "$ENVIRONMENT" --body "$SUBSCRIPTION_ID" --repo "$REPO"
gh variable set AZURE_RESOURCE_GROUP --env "$ENVIRONMENT" --body "$RESOURCE_GROUP" --repo "$REPO"

echo "Setting AZCLIPS_GH_TOKEN secret (needs read access to Azure/azclips)..."
gh auth token | gh secret set AZCLIPS_GH_TOKEN --env "$ENVIRONMENT" --repo "$REPO"

echo ""
echo "Done. Resource group '$RESOURCE_GROUP' / identity '$IDENTITY_NAME' / environment '$ENVIRONMENT' are ready."
echo "clientId=$CLIENT_ID tenantId=$TENANT_ID subscriptionId=$SUBSCRIPTION_ID"
