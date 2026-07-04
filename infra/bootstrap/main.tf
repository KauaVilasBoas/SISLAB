data "aws_caller_identity" "current" {}

locals {
  # Nome do bucket de state precisa ser globalmente único → sufixo com o account id.
  state_bucket_name = "${var.project}-tfstate-${data.aws_caller_identity.current.account_id}"
}

# ----------------------------------------------------------------------------
# Bucket S3 que guarda o state remoto dos demais ambientes (envs/staging etc.).
# Usamos o LOCK NATIVO do backend S3 (use_lockfile) — Terraform >= 1.11 —,
# então NÃO precisamos de tabela DynamoDB de lock. Menos recurso, menos custo.
# ----------------------------------------------------------------------------
resource "aws_s3_bucket" "tfstate" {
  bucket = local.state_bucket_name

  # Protege contra destruição acidental do bucket que contém TODO o state.
  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_versioning" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "tfstate" {
  bucket                  = aws_s3_bucket.tfstate.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# ----------------------------------------------------------------------------
# AWS Budget — proteção de custo. Free tier NÃO é custo zero garantido; este
# orçamento envia e-mail ao atingir 80% (real) e 100% (previsto) do teto.
# ----------------------------------------------------------------------------
resource "aws_budgets_budget" "monthly" {
  count        = var.create_budget ? 1 : 0
  name         = "${var.project}-monthly"
  budget_type  = "COST"
  limit_amount = var.budget_limit_usd
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.budget_notification_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.budget_notification_email]
  }
}
