#!/usr/bin/env bash
#
# change.sh — STAGE 1 (run on your LOCAL machine).
#
# Provisions/updates the Azure resources for this module via Terraform.
#
# This stage does NOT log in to az or run `vm show` — managed-identity auth
# and the vm-show smoke test must run ON the VM (see scripts/setup-vm-mi.sh).
#
# Terraform uses its own Azure credentials (az login / ARM_* env vars), which
# are independent of az.
#
# Usage:
#   ./change.sh
#
set -euo pipefail

# Always operate from this script's directory (the terraform module).
cd "$(dirname "$0")"

EXPECTED_TENANT_ID="4f00b3b6-2940-4f2c-b037-94637c180d30"
EXPECTED_SUBSCRIPTION_ID="f758ac53-3e63-4317-a956-0997793808d7"
BACKEND_RESOURCE_GROUP="ManagedIdentityLiveTest"
BACKEND_STORAGE_ACCOUNT="cirrustfstatef758ac53"
BACKEND_CONTAINER="tfstate"

current_tenant_id="$(az account show --query tenantId -o tsv | tr -d '\r')"
current_subscription_id="$(az account show --query id -o tsv | tr -d '\r')"

if [[ "$current_tenant_id" != "$EXPECTED_TENANT_ID" || "$current_subscription_id" != "$EXPECTED_SUBSCRIPTION_ID" ]]; then
  echo "!! Azure CLI must target the Azure Client Tools 2026-07-20 tenant." >&2
  echo "   Expected tenant:       $EXPECTED_TENANT_ID" >&2
  echo "   Expected subscription: $EXPECTED_SUBSCRIPTION_ID" >&2
  echo "   Current tenant:        $current_tenant_id" >&2
  echo "   Current subscription:  $current_subscription_id" >&2
  exit 1
fi

if ! az storage account show \
  --name "$BACKEND_STORAGE_ACCOUNT" \
  --resource-group "$BACKEND_RESOURCE_GROUP" \
  -o none 2>/dev/null; then
  backend_location="$(az group show --name "$BACKEND_RESOURCE_GROUP" --query location -o tsv | tr -d '\r')"
  echo "==> Creating Terraform state account $BACKEND_STORAGE_ACCOUNT"
  az storage account create \
    --name "$BACKEND_STORAGE_ACCOUNT" \
    --resource-group "$BACKEND_RESOURCE_GROUP" \
    --location "$backend_location" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --min-tls-version TLS1_2 \
    --allow-blob-public-access false \
    -o none
fi

export ARM_ACCESS_KEY
ARM_ACCESS_KEY="$(az storage account keys list \
  --account-name "$BACKEND_STORAGE_ACCOUNT" \
  --resource-group "$BACKEND_RESOURCE_GROUP" \
  --query '[0].value' -o tsv | tr -d '\r')"

az storage container create \
  --name "$BACKEND_CONTAINER" \
  --account-name "$BACKEND_STORAGE_ACCOUNT" \
  --account-key "$ARM_ACCESS_KEY" \
  -o none

echo "==> terraform init"
terraform init -input=false -reconfigure

echo "==> terraform validate"
terraform validate

# Detect whether anything actually needs to change before applying. With
# -detailed-exitcode: 0 = no changes, 1 = error, 2 = changes present. This keeps
# re-runs fast (skips apply entirely when the infrastructure already matches).
echo "==> terraform plan (checking for pending changes)"
set +e
terraform plan -input=false -detailed-exitcode -out=change.tfplan
plan_rc=$?
set -e

case "$plan_rc" in
  0)
    echo "==> No changes — infrastructure is up to date. Skipping apply."
    rm -f change.tfplan
    ;;
  2)
    echo "==> Applying pending changes (auto-approve)"
    terraform apply -input=false -auto-approve change.tfplan
    rm -f change.tfplan
    ;;
  *)
    echo "!! terraform plan failed (exit $plan_rc)." >&2
    rm -f change.tfplan
    exit "$plan_rc"
    ;;
esac

echo "==> Done. Next: build the bundle with scripts/publish-vm-mi.sh, copy it to the"
echo "    VM, then run scripts/setup-vm-mi.sh --test-mi on the VM to verify MI + vm show."
