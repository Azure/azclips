terraform {
  # Pinned to the 1.9 series — see the note in tests/Live/Terraform/linux-vm/versions.tf.
  # Stage 0 keeps its own state, but the same one-way version stamp applies to
  # it, and it is the harder one to recover: it holds the CI identity that every
  # later stage authenticates as. Keep in step with linux-vm and the
  # terraform_version pin in .github/workflows/mi-live.yml.
  required_version = "~> 1.9.0"

  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # State for stage 0 is deliberately LOCAL, unlike tests/Live/Terraform/linux-vm.
  #
  # Two reasons. First, the CI identity created here must never be refreshed (or
  # destroyed) by the CI runs that authenticate as it, and stage 0 only ever runs
  # from a developer machine, so nothing else needs to read this state.
  #
  # Second, and decisive: everything this module creates is an Entra directory
  # object or a subscription-scoped role assignment, so none of it lives in a
  # resource group. In tenants with a policy that deletes resource groups on a
  # schedule, the resources survive but a remote state blob would not — leaving
  # Terraform with empty state and live objects it no longer tracks. Re-running
  # would then create a duplicate app registration (Entra display names are not
  # unique) and repoint AZURE_CLIENT_ID at it. Local state has the same lifetime
  # as the resources it describes.
  #
  # The state file is per-developer and gitignored (see .gitignore). Losing the
  # working copy orphans the identity: recover by importing the four objects, or
  # delete them and re-run.
}

provider "azuread" {
  tenant_id = var.tenant_id
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id
}
