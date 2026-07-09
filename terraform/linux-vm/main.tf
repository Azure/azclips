# Reference the EXISTING resource group. Using a data source (not a resource)
# guarantees this template never creates, modifies, or deletes the group itself
# — it only deploys resources *into* the group.
data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

locals {
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
}

resource "azurerm_virtual_network" "vnet" {
  name                = "${var.vm_name}-vnet"
  resource_group_name = local.resource_group_name
  location            = local.location
  address_space       = ["10.0.0.0/16"]
  tags                = var.tags
}

resource "azurerm_subnet" "subnet" {
  name                 = "${var.vm_name}-subnet"
  resource_group_name  = local.resource_group_name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.1.0/24"]
}

resource "azurerm_public_ip" "pip" {
  name                = "${var.vm_name}-pip"
  resource_group_name = local.resource_group_name
  location            = local.location
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = var.tags
}

resource "azurerm_network_security_group" "nsg" {
  name                = "${var.vm_name}-nsg"
  resource_group_name = local.resource_group_name
  location            = local.location
  tags                = var.tags

  # Inbound SSH is opened only when `allowed_ssh_source_cidr` is set; otherwise no
  # inbound rules are emitted (equivalent to the previous `security_rule = []`) and
  # the default NSG rules deny inbound Internet traffic. `security_rule` is a computed
  # attribute, so a zero-iteration dynamic block sets it to an empty list to REMOVE any
  # pre-existing rules. Outbound to Azure Storage (to pull the test bundle) stays allowed
  # by the default rules. When set, the E2E test can still use `az vm run-command`.
  dynamic "security_rule" {
    for_each = var.allowed_ssh_source_cidr != "" ? [1] : []
    content {
      name                       = "AllowSSHInbound"
      priority                   = 1000
      direction                  = "Inbound"
      access                     = "Allow"
      protocol                   = "Tcp"
      source_port_range          = "*"
      destination_port_range     = "22"
      source_address_prefix      = var.allowed_ssh_source_cidr
      destination_address_prefix = "*"
    }
  }
}

resource "azurerm_network_interface" "nic" {
  name                = "${var.vm_name}-nic"
  resource_group_name = local.resource_group_name
  location            = local.location
  tags                = var.tags

  ip_configuration {
    name                          = "internal"
    subnet_id                     = azurerm_subnet.subnet.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.pip.id
  }
}

resource "azurerm_network_interface_security_group_association" "nic_nsg" {
  network_interface_id      = azurerm_network_interface.nic.id
  network_security_group_id = azurerm_network_security_group.nsg.id
}

# create/update: terraform apply will create the VM if absent, or update it
# in place (and recreate when an immutable field changes).
resource "azurerm_linux_virtual_machine" "vm" {
  name                = var.vm_name
  resource_group_name = local.resource_group_name
  location            = local.location
  size                = var.vm_size
  admin_username      = var.admin_username
  tags                = var.tags

  # Managed identities used by `az login --identity` on the VM (via IMDS):
  #   - SystemAssigned: the VM's own identity (default `login --identity`).
  #   - UserAssigned:   user-assigned-mi, selected with `--client-id`/`--resource-id`.
  # Declared here so Terraform preserves them rather than stripping on apply.
  identity {
    type         = "SystemAssigned, UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.uami.id]
  }

  network_interface_ids = [
    azurerm_network_interface.nic.id,
  ]

  admin_ssh_key {
    username = var.admin_username
    # Prefer the caller-provided key (so you can SSH in with the matching private
    # key); fall back to the generated throwaway key when none is supplied.
    public_key = var.ssh_public_key != "" ? var.ssh_public_key : tls_private_key.vm_admin.public_key_openssh
  }

  # SSH-key only; no password login. When `ssh_public_key` is set, inbound SSH can
  # be opened via `allowed_ssh_source_cidr` and you can connect with your private
  # key. Otherwise the keypair is generated below purely to satisfy Azure's
  # requirement that a Linux VM has an admin credential, and the CI test drives the
  # VM through `az vm run-command` with port 22 closed.
  disable_password_authentication = true

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = var.os_disk_type
  }

  source_image_reference {
    publisher = var.image.publisher
    offer     = var.image.offer
    sku       = var.image.sku
    version   = var.image.version
  }

  # The admin SSH key is a throwaway credential that is never used to connect
  # (the test drives the VM via `az vm run-command`). Ignore changes to it so
  # switching the key source never forces a destructive VM replacement — and so
  # it never collides with the resource group's CanNotDelete lock. New VMs still
  # get the generated key; existing VMs keep whatever key they were created with.
  lifecycle {
    ignore_changes = [admin_ssh_key]
  }
}

# Fallback SSH keypair, used only when `ssh_public_key` is not provided, to satisfy
# Azure's requirement that a Linux VM be created with an admin credential. In that
# case it is NOT used to connect (CI drives the VM via `az vm run-command`, port 22
# stays closed, and the private key stays in Terraform state, never output). Provide
# `ssh_public_key` to use your own key and SSH in instead.
resource "tls_private_key" "vm_admin" {
  algorithm = "RSA"
  rsa_bits  = 4096
}

# User-assigned managed identity attached to the VM (see the identity block above).
# Logged into on the VM with: az login --identity --client-id <client_id>.
resource "azurerm_user_assigned_identity" "uami" {
  name                = var.user_assigned_identity_name
  resource_group_name = local.resource_group_name
  location            = local.location
}

# Grant the user-assigned identity an RBAC role at the resource-group scope so it
# can actually read/operate on resources once authenticated (e.g. `az vm show`).
resource "azurerm_role_assignment" "uami_role" {
  scope                = data.azurerm_resource_group.rg.id
  role_definition_name = var.uami_role
  principal_id         = azurerm_user_assigned_identity.uami.principal_id
}

# The system-assigned identity is also exercised by the E2E test and needs
# permission to read the VM through ARM after authenticating through IMDS.
resource "azurerm_role_assignment" "system_assigned_identity_role" {
  scope                = data.azurerm_resource_group.rg.id
  role_definition_name = var.system_assigned_identity_role
  principal_id         = azurerm_linux_virtual_machine.vm.identity[0].principal_id
}

# CanNotDelete management lock on the resource group. Protects the group (and
# everything in it) from accidental deletion. depends_on ensures Terraform removes
# this lock before the resources it guards during a destroy/replace, avoiding the
# 409 ScopeLocked error that an in-place lock would otherwise cause.
resource "azurerm_management_lock" "rg_lock" {
  name       = var.resource_lock_name
  scope      = data.azurerm_resource_group.rg.id
  lock_level = "CanNotDelete"
  notes      = ""

  depends_on = [
    azurerm_linux_virtual_machine.vm,
    azurerm_network_interface_security_group_association.nic_nsg,
    azurerm_network_interface.nic,
    azurerm_network_security_group.nsg,
    azurerm_subnet.subnet,
    azurerm_virtual_network.vnet,
    azurerm_public_ip.pip,
    azurerm_user_assigned_identity.uami,
    azurerm_role_assignment.uami_role,
    azurerm_role_assignment.system_assigned_identity_role,
  ]
}
