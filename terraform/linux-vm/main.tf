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

  security_rule {
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
    username   = var.admin_username
    public_key = var.ssh_public_key
  }

  # SSH-key only; no password login.
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
  ]
}
