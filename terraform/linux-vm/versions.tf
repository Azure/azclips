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

  # Remote state so local runs and CI (mi-e2e.yml) share the same state.
  # change.sh creates the backend when needed and supplies ARM_ACCESS_KEY.
  backend "azurerm" {
    resource_group_name  = "ManagedIdentityLiveTest"
    storage_account_name = "cirrustfstatef758ac53"
    container_name       = "tfstate"
    key                  = "linux-vm.tfstate"
  }
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id
}
