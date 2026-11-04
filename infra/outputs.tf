output "function_hostnames" {
  value = { for key, app in azurerm_linux_function_app.main : key => app.default_hostname }
}

output "function_names" {
  value = { for key, app in azurerm_linux_function_app.main : key => app.name }
}
