locals {
  ssm_prefix = "/${var.project}/${var.env}"
}

# Usamos a VPC default e suas subnets (públicas) — sem custo de NAT/EIP.
data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

module "network" {
  source  = "../../modules/network"
  project = var.project
  env     = var.env
  vpc_id  = data.aws_vpc.default.id
}

module "storage" {
  source  = "../../modules/storage"
  project = var.project
  env     = var.env
}

module "database" {
  source     = "../../modules/database"
  project    = var.project
  env        = var.env
  subnet_ids = data.aws_subnets.default.ids
  db_sg_id   = module.network.db_sg_id
  ssm_prefix = local.ssm_prefix
}

module "compute" {
  source                 = "../../modules/compute"
  project                = var.project
  env                    = var.env
  vpc_id                 = data.aws_vpc.default.id
  subnet_ids             = data.aws_subnets.default.ids
  app_sg_id              = module.network.app_sg_id
  ssm_prefix             = local.ssm_prefix
  attachments_bucket_arn = module.storage.bucket_arn
  aws_region             = var.aws_region
}
