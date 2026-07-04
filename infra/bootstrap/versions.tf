# Bootstrap — cria os recursos que sustentam o restante do Terraform.
# Este módulo usa STATE LOCAL (não há backend remoto ainda; ele é quem cria o bucket
# de state). Rode com `terraform init && terraform apply` uma única vez.

terraform {
  required_version = ">= 1.11"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
      Component = "bootstrap"
    }
  }
}
