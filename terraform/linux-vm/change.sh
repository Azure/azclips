#!/usr/bin/env bash
#
# change.sh — STAGE 1 (run on your LOCAL machine).
#
# Provisions/updates the Azure resources for this module via Terraform. With
# vm_name = "test-vm-mi", apply renames the resources: it DESTROYS the old
# ming-test-vm* resources and CREATES test-vm-mi* in their place.
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

echo "==> terraform init"
terraform init -input=false

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
