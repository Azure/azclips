terraform {
  # Pinned to the 1.9 series (>= 1.9.0, < 1.10.0), not just a lower bound.
  # Terraform records the version that last wrote a state file and then refuses
  # to read state written by anything newer, so a single run on a newer binary
  # permanently locks out everyone still on the old one. This state is shared
  # between CI and every developer using this alias, so the constraint has to
  # fail fast at `init` rather than let that happen. Note `~> 1.9` would NOT
  # do — it permits 1.15. Changing this is a deliberate, coordinated upgrade:
  # bump it here, in tests/Live/Terraform/bootstrap/versions.tf, and in the
  # terraform_version pin in .github/workflows/mi-live.yml together.
  required_version = "~> 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }

  # Remote state so local runs and CI (mi-live.yml) share the same state.
  # The blob key is per-developer and supplied by provision-infrastructure.sh via
  # -backend-config: backend blocks are evaluated before variables exist, so the
  # key cannot interpolate var.alias here. Without that, every developer would
  # write to one state file and overwrite each other's infrastructure.
  # resource_group_name is intentionally omitted: the backend does not need it
  # when ARM_ACCESS_KEY is supplied (provision-infrastructure.sh does), and
  # omitting it means the state account can live in any group without this file
  # having to agree.
  # storage_account_name and key both come from -backend-config: the account is
  # derived from the active subscription, so it cannot be written down here.
  backend "azurerm" {
    container_name = "tfstate"
  }
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id
}
