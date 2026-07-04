# RDS PostgreSQL db.t3.micro (free tier: 750h/mês + 20GB). Single-AZ, privado.
# A senha é gerada aqui e guardada no SSM Parameter Store (SecureString) —
# nunca fica em texto claro no código nem no output.

variable "project" { type = string }
variable "env" { type = string }
variable "subnet_ids" { type = list(string) }
variable "db_sg_id" { type = string }
variable "ssm_prefix" { type = string }

variable "instance_class" {
  type    = string
  default = "db.t3.micro" # free tier eligible
}

variable "allocated_storage" {
  type    = number
  default = 20 # limite do free tier
}

variable "engine_version" {
  type    = string
  default = "16"
}

variable "db_name" {
  type    = string
  default = "sislab"
}

variable "username" {
  type    = string
  default = "sislab_app"
}

resource "random_password" "db" {
  length  = 24
  special = true
  # RDS não aceita estes caracteres na senha do master:
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "aws_db_subnet_group" "this" {
  name       = "${var.project}-${var.env}"
  subnet_ids = var.subnet_ids
  tags       = { Name = "${var.project}-${var.env}" }
}

resource "aws_db_instance" "this" {
  identifier     = "${var.project}-${var.env}"
  engine         = "postgres"
  engine_version = var.engine_version
  instance_class = var.instance_class

  allocated_storage = var.allocated_storage
  storage_type      = "gp2"
  storage_encrypted = true

  db_name  = var.db_name
  username = var.username
  password = random_password.db.result

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.db_sg_id]
  publicly_accessible    = false
  multi_az               = false # Multi-AZ estoura o free tier

  backup_retention_period = 7
  skip_final_snapshot     = true # staging; em prod, mudar para false
  deletion_protection     = false
  apply_immediately       = true

  tags = { Name = "${var.project}-${var.env}" }
}

# --------- Segredos e configuração no SSM Parameter Store ---------
resource "aws_ssm_parameter" "db_host" {
  name  = "${var.ssm_prefix}/db/host"
  type  = "String"
  value = aws_db_instance.this.address
}

resource "aws_ssm_parameter" "db_port" {
  name  = "${var.ssm_prefix}/db/port"
  type  = "String"
  value = tostring(aws_db_instance.this.port)
}

resource "aws_ssm_parameter" "db_name" {
  name  = "${var.ssm_prefix}/db/name"
  type  = "String"
  value = var.db_name
}

resource "aws_ssm_parameter" "db_username" {
  name  = "${var.ssm_prefix}/db/username"
  type  = "String"
  value = var.username
}

resource "aws_ssm_parameter" "db_password" {
  name  = "${var.ssm_prefix}/db/password"
  type  = "SecureString"
  value = random_password.db.result
}

# Connection string pronta para o Npgsql/EF, também como SecureString.
resource "aws_ssm_parameter" "db_connection_string" {
  name = "${var.ssm_prefix}/db/connection-string"
  type = "SecureString"
  value = format(
    "Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;SSL Mode=Require;Trust Server Certificate=true",
    aws_db_instance.this.address,
    aws_db_instance.this.port,
    var.db_name,
    var.username,
    random_password.db.result,
  )
}

output "db_endpoint" { value = aws_db_instance.this.address }
output "ssm_connection_string_name" { value = aws_ssm_parameter.db_connection_string.name }
output "ssm_prefix" { value = var.ssm_prefix }
