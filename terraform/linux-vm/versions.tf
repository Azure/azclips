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
  # Auth uses Azure AD (no account keys); CI passes ARM_USE_OIDC=true.
  backend "azurerm" {
    resource_group_name  = "ming-test"
    storage_account_name = "cirrustfstate0c302431"
    container_name       = "tfstate"
    key                  = "linux-vm.tfstate"
    use_azuread_auth     = true
  }
}

provider "azurerm" {
  features {}

  subscription_id = var.subscription_id
}
