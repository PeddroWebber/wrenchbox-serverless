variable "resource_group_name" {
  type        = string
  description = "Resource group criado pelo repositório do banco."
  default     = "rg-wrenchbox"
}

variable "key_vault_name" {
  type        = string
  description = "Key Vault criado pelo repositório do banco."
}

variable "environments" {
  type    = set(string)
  default = ["homolog", "prod"]
}
