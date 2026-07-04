variable "aws_region" {
  description = "Região AWS onde os recursos são criados."
  type        = string
  default     = "sa-east-1"
}

variable "project" {
  description = "Nome do projeto, usado como prefixo/tag dos recursos."
  type        = string
  default     = "sislab"
}

variable "create_budget" {
  description = "Se o AWS Budget deve ser criado pelo Terraform. Desligado por padrão porque o budget já foi criado manualmente no console."
  type        = bool
  default     = false
}

variable "budget_limit_usd" {
  description = "Teto mensal de custo (USD) para o alerta de billing. Free tier deve ficar ~0, mas o alarme protege contra surpresas."
  type        = string
  default     = "5"
}

variable "budget_notification_email" {
  description = "E-mail que recebe os alertas de orçamento (80% e 100% do teto). Só usado se create_budget = true."
  type        = string
  default     = ""
}
