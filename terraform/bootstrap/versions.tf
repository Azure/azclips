terraform {
  # Pinned to the 1.9 series — see the note in terraform/linux-vm/versions.tf.
  # Stage 0 has its own state blob, but the same one-way version stamp applies
  # to it, and it is the harder one to recover: it holds the CI identity that
  # every later stage authenticates as. Keep in step with linux-vm and the
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

  # State for stage 0 is deliberately SEPARATE from terraform/linux-vm: the CI
  # identity created here must never be refreshed (or destroyed) by the CI runs
  # that authenticate as it. The blob key is per-developer and supplied by
  # setup-github-identity.sh via -backend-config, because backend blocks are
  # evaluated before variables exist and so cannot interpolate var.alias.
  # resource_group_name is intentionally omitted: the backend does not need it
  # when ARM_ACCESS_KEY is supplied (setup-github-identity.sh does), and omitting
  # it means the state account can live in any group without this file having to
  # agree.
  # storage_account_name and key both come from -backend-config: the account is
  # derived from the active subscription, so it cannot be written down here.
  backend "azurerm" {
    container_name = "tfstate"
  }
}

provider "azuread" {
  tenant_id = var.tenant_id
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id
}
