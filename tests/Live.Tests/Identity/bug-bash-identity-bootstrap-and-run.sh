#!/usr/bin/env bash
# tears down leftovers, bootstraps fresh resources, triggers the workflow, and watches it.
# usage: ./tests/Live.Tests/Identity/bug-bash-identity-bootstrap-and-run.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=bug-bash-identity-env.sh
source "$SCRIPT_DIR/bug-bash-identity-env.sh"

RESOURCE_GROUP="bug-bash-identity-$ALIAS"
BRANCH="$(git rev-parse --abbrev-ref HEAD)"
WORKFLOW="bug-bash-identity.yml"
ENVIRONMENT="bug-bash-identity"

if [[ "$BRANCH" == "HEAD" ]]; then
  echo "You're in a detached HEAD state. Check out a branch (pushed to your fork) first." >&2
  exit 1
fi

echo "=== Removing any existing resource group and GitHub environment ==="
# blocking delete - don't want to race a resource group that's still deleting
az group delete --name "$RESOURCE_GROUP" --yes 2>/dev/null || true
gh api -X DELETE "repos/$REPO/environments/$ENVIRONMENT" --silent 2>/dev/null || true

echo ""
echo "=== Bootstrapping Azure + GitHub environment ==="
"$SCRIPT_DIR/bug-bash-identity-bootstrap.sh"

# give the env vars/secret a moment to propagate before dispatching
echo "Waiting for GitHub environment secrets/variables to propagate..."
sleep 20

echo ""
echo "=== Triggering workflow '$WORKFLOW' on branch '$BRANCH' ==="
before_run_id="$(gh run list --workflow="$WORKFLOW" --repo "$REPO" --branch "$BRANCH" --limit 1 --json databaseId -q '.[0].databaseId' 2>/dev/null || true)"

gh workflow run "$WORKFLOW" --ref "$BRANCH" --repo "$REPO"

echo "Waiting for the new run to appear..."
run_id=""
for _ in $(seq 1 30); do
  sleep 3
  candidate="$(gh run list --workflow="$WORKFLOW" --repo "$REPO" --branch "$BRANCH" --limit 1 --json databaseId -q '.[0].databaseId')"
  if [[ -n "$candidate" && "$candidate" != "$before_run_id" ]]; then
    run_id="$candidate"
    break
  fi
done

if [[ -z "$run_id" ]]; then
  echo "Timed out waiting for the new workflow run to appear." >&2
  exit 1
fi

echo "=== Watching run $run_id ==="
gh run watch "$run_id" --repo "$REPO" --exit-status
