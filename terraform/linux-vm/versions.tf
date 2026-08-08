terraform {
  required_version = ">= 1.3.0"

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
