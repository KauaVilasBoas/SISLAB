# Backend remoto no S3 com LOCK NATIVO (use_lockfile) — sem DynamoDB.
# O nome do bucket sai do `bootstrap` (inclui o account id), então ele é
# preenchido no init via -backend-config. Exemplo:
#
#   terraform init \
#     -backend-config="bucket=sislab-tfstate-<ACCOUNT_ID>"
#
terraform {
  backend "s3" {
    key          = "staging/terraform.tfstate"
    region       = "sa-east-1"
    encrypt      = true
    use_lockfile = true
  }
}
