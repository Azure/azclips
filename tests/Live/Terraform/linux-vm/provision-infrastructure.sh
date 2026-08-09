#!/usr/bin/env bash
#
# provision-infrastructure.sh — STAGE 1.
#
# Creates/updates the per-developer Azure resources for the managed-identity live
# test: a resource group, a Linux VM, and the system- and user-assigned
# identities the test exercises.
#
# Runs BOTH locally and in CI (.github/workflows/mi-live.yml invokes it through
# `dotnet run --project tests/Live/Tool -- --provision`). Locally the alias comes from
# terraform.tfvars; in CI it comes from TF_VAR_alias, fed by the ALIAS GitHub
# environment variable that stage 0 published.
#
# Stage 0 (tests/Live/Terraform/bootstrap/setup-github-identity.sh) must have run first if
# you intend to use the GitHub workflow — it creates the identity CI logs in as.
# It is not needed for purely local runs.
#
# This stage does NOT log in to az or run `vm show` — managed-identity auth and
# the vm-show smoke test must run ON the VM (see tests/Live/Scripts/setup-vm-mi.sh).
#
# Terraform uses its own Azure credentials (az login / ARM_* env vars), which are
# independent of az.
#
# Usage:
#   ./provision-infrastructure.sh
set -euo pipefail

# Always operate from this script's directory (the terraform module).
cd "$(dirname "$0")"
MODULE_DIR="$PWD"

# shellcheck source=../tf-common.sh
source "$MODULE_DIR/../tf-common.sh"

# Fail fast: wrong tenant/subscription is the most common misconfiguration, and
# everything after this point either creates real resources or fails obscurely.
# Expected values come from tf-common.sh.
require_az_context

# Terraform targets whatever subscription the CLI is on, rather than a
# hardcoded one, so each developer works in their own subscription.
export TF_VAR_tenant_id="$AZ_TENANT_ID"
export TF_VAR_subscription_id="$AZ_SUBSCRIPTION_ID"

ALIAS="$(resolve_alias "$MODULE_DIR")"
check_alias_drift "$MODULE_DIR" "$ALIAS"
export TF_VAR_alias="$ALIAS"
echo "==> Alias: $ALIAS"

# Terraform state, named from the alias so each developer's state is separate
# and recognisable. The account is located by name, so it is found wherever it
# already lives; the group below is only used if it has to be created.
BACKEND_STORAGE_ACCOUNT="$(state_account_name "$ALIAS")"
BACKEND_FALLBACK_RESOURCE_GROUP="$(backend_resource_group "$ALIAS")"

ensure_state_backend "$BACKEND_STORAGE_ACCOUNT" "$BACKEND_CONTAINER" "$BACKEND_FALLBACK_RESOURCE_GROUP" "$BACKEND_LOCATION"

echo "==> terraform init (state: ${ALIAS}-linux-vm.tfstate)"
terraform init -input=false -reconfigure \
  -backend-config="storage_account_name=${BACKEND_STORAGE_ACCOUNT}" \
  -backend-config="key=${ALIAS}-linux-vm.tfstate"

echo "==> terraform validate"
terraform validate

plan_and_apply "provision.tfplan"

record_alias "$MODULE_DIR" "$ALIAS"

echo "==> Done. Next: run the live test with: dotnet run --project tests/Live/Tool"
echo "    VM, then run tests/Live/Scripts/setup-vm-mi.sh --test-mi on the VM to verify MI + vm show."
