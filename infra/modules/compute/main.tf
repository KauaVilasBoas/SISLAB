# Elastic Beanstalk hospedando a API .NET (e o worker de Jobs no mesmo processo).
# Tipo SingleInstance (SEM load balancer) para ficar no free tier: 1x EC2 t3.micro.

variable "project" { type = string }
variable "env" { type = string }
variable "vpc_id" { type = string }
variable "subnet_ids" { type = list(string) }
variable "app_sg_id" { type = string }
variable "ssm_prefix" { type = string }
variable "attachments_bucket_arn" { type = string }
variable "aws_region" { type = string }

variable "instance_type" {
  type    = string
  default = "t3.micro" # free tier
}

# Plataforma gerenciada .NET 8 no Amazon Linux 2023 (versão mais recente).
data "aws_elastic_beanstalk_solution_stack" "dotnet" {
  most_recent = true
  name_regex  = "^64bit Amazon Linux 2023 (.*) running .NET 8$"
}

# ------------------------- IAM: role da instância EC2 -------------------------
data "aws_iam_policy_document" "ec2_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "instance" {
  name               = "${var.project}-${var.env}-eb-instance"
  assume_role_policy = data.aws_iam_policy_document.ec2_assume.json
}

# Permissões base do EB Web Tier.
resource "aws_iam_role_policy_attachment" "web_tier" {
  role       = aws_iam_role.instance.name
  policy_arn = "arn:aws:iam::aws:policy/AWSElasticBeanstalkWebTier"
}

# Leitura dos parâmetros do app no SSM (inclui SecureString → precisa de kms:Decrypt).
data "aws_iam_policy_document" "app_runtime" {
  statement {
    sid       = "ReadSsmParameters"
    actions   = ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"]
    resources = ["arn:aws:ssm:${var.aws_region}:*:parameter${var.ssm_prefix}/*"]
  }
  statement {
    sid       = "DecryptSecureStrings"
    actions   = ["kms:Decrypt"]
    resources = ["*"] # chave gerenciada aws/ssm; pode ser restringida ao ARN da chave depois
  }
  statement {
    sid       = "AttachmentsBucketAccess"
    actions   = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:ListBucket"]
    resources = [var.attachments_bucket_arn, "${var.attachments_bucket_arn}/*"]
  }
}

resource "aws_iam_role_policy" "app_runtime" {
  name   = "${var.project}-${var.env}-app-runtime"
  role   = aws_iam_role.instance.id
  policy = data.aws_iam_policy_document.app_runtime.json
}

resource "aws_iam_instance_profile" "instance" {
  name = "${var.project}-${var.env}-eb-instance"
  role = aws_iam_role.instance.name
}

# ------------------------- IAM: service role do EB -------------------------
data "aws_iam_policy_document" "eb_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["elasticbeanstalk.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "service" {
  name               = "${var.project}-${var.env}-eb-service"
  assume_role_policy = data.aws_iam_policy_document.eb_assume.json
}

resource "aws_iam_role_policy_attachment" "service_health" {
  role       = aws_iam_role.service.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSElasticBeanstalkEnhancedHealth"
}

resource "aws_iam_role_policy_attachment" "service_updates" {
  role       = aws_iam_role.service.name
  policy_arn = "arn:aws:iam::aws:policy/AWSElasticBeanstalkManagedUpdatesCustomerRolePolicy"
}

# ------------------------- Aplicação e ambiente EB -------------------------
resource "aws_elastic_beanstalk_application" "this" {
  name        = "${var.project}-api"
  description = "SISLAB API (.NET) + Jobs"
}

resource "aws_elastic_beanstalk_environment" "this" {
  name                = "${var.project}-${var.env}"
  application         = aws_elastic_beanstalk_application.this.name
  solution_stack_name = data.aws_elastic_beanstalk_solution_stack.dotnet.name
  tier                = "WebServer"

  # Sem load balancer → free tier.
  setting {
    namespace = "aws:elasticbeanstalk:environment"
    name      = "EnvironmentType"
    value     = "SingleInstance"
  }
  setting {
    namespace = "aws:elasticbeanstalk:environment"
    name      = "ServiceRole"
    value     = aws_iam_role.service.arn
  }

  # Rede: VPC default, subnet pública, IP público (SingleInstance).
  setting {
    namespace = "aws:ec2:vpc"
    name      = "VPCId"
    value     = var.vpc_id
  }
  setting {
    namespace = "aws:ec2:vpc"
    name      = "Subnets"
    value     = join(",", var.subnet_ids)
  }
  setting {
    namespace = "aws:ec2:vpc"
    name      = "AssociatePublicIpAddress"
    value     = "true"
  }

  # Instância.
  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "IamInstanceProfile"
    value     = aws_iam_instance_profile.instance.name
  }
  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "InstanceType"
    value     = var.instance_type
  }
  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "SecurityGroups"
    value     = var.app_sg_id
  }

  # Variáveis de ambiente da aplicação. O app resolve a connection string e demais
  # segredos lendo o SSM neste prefixo no startup (não injetamos segredo aqui).
  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "ASPNETCORE_ENVIRONMENT"
    value     = "Staging"
  }
  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "AWS__Region"
    value     = var.aws_region
  }
  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Sislab__Ssm__Prefix"
    value     = var.ssm_prefix
  }
}

output "environment_cname" { value = aws_elastic_beanstalk_environment.this.cname }
output "environment_name" { value = aws_elastic_beanstalk_environment.this.name }
output "application_name" { value = aws_elastic_beanstalk_application.this.name }
