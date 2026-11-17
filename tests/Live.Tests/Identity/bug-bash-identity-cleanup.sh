#!/usr/bin/env bash
# deletes everything bug-bash-identity-<alias> created: resource group (cascades identity +
# federated credential), GitHub environment. leaves the workflow file itself in place.
# usage: ./tests/Live.Tests/Identity/bug-bash-identity-cleanup.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=bug-bash-identity-env.sh
source "$SCRIPT_DIR/bug-bash-identity-env.sh"

RESOURCE_GROUP="bug-bash-identity-$ALIAS"
ENVIRONMENT="bug-bash-identity"

echo "Deleting Azure resource group '$RESOURCE_GROUP' (and everything in it)..."
az group delete --name "$RESOURCE_GROUP" --yes --no-wait

echo "Deleting GitHub environment '$ENVIRONMENT' on $REPO..."
gh api -X DELETE "repos/$REPO/environments/$ENVIRONMENT" 2>/dev/null || true

echo "Done. Resource group deletion continues in the background (--no-wait);"
echo "check with: az group exists --name $RESOURCE_GROUP"
echo ""
echo "Workflow file (.github/workflows/bug-bash-identity.yml) left in place;"
echo "remove it manually (git rm) if you want the workflow gone too."
