output "api_url" {
  description = "CNAME do ambiente Elastic Beanstalk (endpoint da API)."
  value       = "http://${module.compute.environment_cname}"
}

output "db_endpoint" {
  description = "Endpoint do RDS PostgreSQL (privado)."
  value       = module.database.db_endpoint
}

output "attachments_bucket" {
  description = "Bucket S3 de anexos."
  value       = module.storage.bucket_name
}

output "ssm_prefix" {
  description = "Prefixo dos parâmetros do app no SSM."
  value       = local.ssm_prefix
}
