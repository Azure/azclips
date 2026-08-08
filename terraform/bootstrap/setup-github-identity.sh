#!/usr/bin/env bash
#
# setup-github-identity.sh — STAGE 0. Run ONCE, from your LOCAL machine.
#
# Creates the Entra identity GitHub Actions uses to talk to Azure:
#   - an app registration + service principal
#   - a federated credential trusting this repo's `dev` environment (OIDC, no
#     client secret ever exists)
#   - a Contributor role assignment at subscription scope
#
# then publishes the resulting IDs to GitHub as `dev` environment variables, so
# stage 1 and the workflow need no manual configuration.
#
# This must NEVER run in CI. It creates the very identity CI authenticates as,
# and it needs permissions CI deliberately does not have: Owner on the
# subscription (for the role assignment) plus the right to create Entra
# applications.
#
# Prerequisites:
#   - az login, pointed at the expected tenant/subscription
#   - gh auth login as the owner of your fork, with admin rights on it
#   - terraform.tfvars with `alias` (see terraform.tfvars.example)
#
# Usage:
#   ./setup-github-identity.sh
#
# Afterwards, run stage 1: terraform/linux-vm/provision-infrastructure.sh
set -euo pipefail

cd "$(dirname "$0")"
MODULE_DIR="$PWD"

# shellcheck source=../tf-common.sh
source "$MODULE_DIR/../tf-common.sh"

# Fail fast: wrong tenant/subscription is the most common misconfiguration, and
# this stage creates a long-lived identity, so getting the tenant right matters
# before anything else runs. Expected values come from tf-common.sh.
require_az_context

# Terraform targets whatever subscription the CLI is on, rather than a
# hardcoded one, so each developer works in their own subscription.
export TF_VAR_tenant_id="$AZ_TENANT_ID"
export TF_VAR_subscription_id="$AZ_SUBSCRIPTION_ID"

if ! command -v terraform >/dev/null 2>&1; then
  echo "!! Missing prerequisite: terraform" >&2
  exit 1
fi

# Fail fast for the same reason as the Azure check: this publishes credentials to
# a repository, and the wrong repository means leaking them to the shared fork
# parent. Derives GH_REPO from `origin`.
require_gh_repo
export TF_VAR_github_repo="$GH_REPO"
echo "==> GitHub repository: $GH_REPO"

ALIAS="$(resolve_alias "$MODULE_DIR")"
export TF_VAR_alias="$ALIAS"
check_alias_drift "$MODULE_DIR" "$ALIAS"
echo "==> Alias: $ALIAS"

# Terraform state, named from the alias so each developer's state is separate
# and recognisable. The account is located by name, so it is found wherever it
# already lives; the group below is only used if it has to be created.
BACKEND_STORAGE_ACCOUNT="$(state_account_name "$ALIAS")"
BACKEND_FALLBACK_RESOURCE_GROUP="$(backend_resource_group "$ALIAS")"

# Creating an app registration is not covered by Azure RBAC — it needs either a
# permissive tenant default or an Entra role. Check up front so the failure is
# understandable rather than a bare Graph 403 mid-apply.
if ! can_create_apps="$(az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/policies/authorizationPolicy" \
  --query "defaultUserRolePermissions.allowedToCreateApps" -o tsv 2>/dev/null | tr -d '\r')"; then
  echo "==> Could not read the tenant application policy; continuing anyway." >&2
elif [[ "$can_create_apps" == "false" ]]; then
  echo "!! This tenant blocks app registration by regular users, and you may not" >&2
  echo "   have an exempting role. You need Application Developer (or Application" >&2
  echo "   Administrator) in Entra ID, plus Owner on the subscription." >&2
  echo "   Continuing — the apply will fail with a Graph 403 if you lack them." >&2
fi

ensure_state_backend "$BACKEND_STORAGE_ACCOUNT" "$BACKEND_CONTAINER" "$BACKEND_FALLBACK_RESOURCE_GROUP" "$BACKEND_LOCATION"

echo "==> terraform init (state: ${ALIAS}-bootstrap.tfstate)"
terraform init -input=false -reconfigure \
  -backend-config="storage_account_name=${BACKEND_STORAGE_ACCOUNT}" \
  -backend-config="key=${ALIAS}-bootstrap.tfstate"

echo "==> terraform validate"
terraform validate

plan_and_apply "bootstrap.tfplan"

record_alias "$MODULE_DIR" "$ALIAS"

CLIENT_ID="$(terraform output -raw client_id | tr -d '\r')"
TENANT_ID="$(terraform output -raw tenant_id | tr -d '\r')"
SUBSCRIPTION_ID="$(terraform output -raw subscription_id | tr -d '\r')"
GH_ENVIRONMENT="$(terraform output -raw github_environment | tr -d '\r')"
FEDERATED_SUBJECT="$(terraform output -raw federated_subject | tr -d '\r')"

# Same repo the credential trusts (checked by require_gh_repo above), so the
# variables land where the workflow will actually read them.
GITHUB_REPO="$GH_REPO"

echo "==> Ensuring GitHub environment '$GH_ENVIRONMENT' exists in $GITHUB_REPO"
gh api -X PUT "repos/${GITHUB_REPO}/environments/${GH_ENVIRONMENT}" --silent

# These are identifiers, not secrets — the actual authentication is the OIDC
# token exchange. Variables (not secrets) keep them readable for debugging.
echo "==> Publishing environment variables"
gh variable set AZURE_CLIENT_ID       --repo "$GITHUB_REPO" --env "$GH_ENVIRONMENT" --body "$CLIENT_ID"
gh variable set AZURE_TENANT_ID       --repo "$GITHUB_REPO" --env "$GH_ENVIRONMENT" --body "$TENANT_ID"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$GITHUB_REPO" --env "$GH_ENVIRONMENT" --body "$SUBSCRIPTION_ID"
gh variable set ALIAS                 --repo "$GITHUB_REPO" --env "$GH_ENVIRONMENT" --body "$ALIAS"

cat <<EOF

==> Stage 0 complete.

    Client ID:   $CLIENT_ID
    Repository:  $GITHUB_REPO
    Environment: $GH_ENVIRONMENT
    OIDC subject: $FEDERATED_SUBJECT

    The job in .github/workflows/mi-live.yml must declare
    'environment: $GH_ENVIRONMENT' or its OIDC token will not match the subject
    above and the Azure login will fail.

    Next: terraform/linux-vm/provision-infrastructure.sh (stage 1) — use the same
    alias '$ALIAS'.
EOF
