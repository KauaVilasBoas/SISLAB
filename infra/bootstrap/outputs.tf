output "state_bucket_name" {
  description = "Nome do bucket S3 de state remoto. Use este valor no backend de envs/staging."
  value       = aws_s3_bucket.tfstate.id
}

output "aws_region" {
  description = "Região onde o state e os recursos vivem."
  value       = var.aws_region
}

output "account_id" {
  description = "ID da conta AWS."
  value       = data.aws_caller_identity.current.account_id
}
