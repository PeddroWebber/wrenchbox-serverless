data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

data "azurerm_key_vault" "main" {
  name                = var.key_vault_name
  resource_group_name = data.azurerm_resource_group.main.name
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

resource "azurerm_storage_account" "main" {
  for_each                 = var.environments
  name                     = "stwb${substr(each.key, 0, 1)}${random_string.suffix.result}"
  resource_group_name      = data.azurerm_resource_group.main.name
  location                 = data.azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_service_plan" "main" {
  for_each            = var.environments
  name                = "plan-wrenchbox-${each.key}-${random_string.suffix.result}"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "Y1"
}

resource "azurerm_linux_function_app" "main" {
  for_each                    = var.environments
  name                        = "func-wrenchbox-${each.key}-${random_string.suffix.result}"
  resource_group_name         = data.azurerm_resource_group.main.name
  location                    = data.azurerm_resource_group.main.location
  service_plan_id             = azurerm_service_plan.main[each.key].id
  storage_account_name        = azurerm_storage_account.main[each.key].name
  storage_account_access_key  = azurerm_storage_account.main[each.key].primary_access_key
  functions_extension_version = "~4"

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME     = "dotnet-isolated"
    Jwt__Issuer                  = "WrenchBox"
    Jwt__Audience                = "WrenchBox.Admin"
    Jwt__CustomerExpiryMinutes   = "30"
    Jwt__Secret                  = "@Microsoft.KeyVault(VaultName=${data.azurerm_key_vault.main.name};SecretName=jwt-secret)"
    ConnectionStrings__Customers = "@Microsoft.KeyVault(VaultName=${data.azurerm_key_vault.main.name};SecretName=customers-auth-connection-${each.key})"
    Smtp__From                   = "WrenchBox <noreply@wrenchbox.local>"
    Smtp__Port                   = "587"
    Smtp__UseSsl                 = "true"
    Notify__Key                  = "@Microsoft.KeyVault(VaultName=${data.azurerm_key_vault.main.name};SecretName=notify-key)"
  }
}

resource "azurerm_role_assignment" "key_vault" {
  for_each             = var.environments
  scope                = data.azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_function_app.main[each.key].identity[0].principal_id
}
