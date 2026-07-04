# Security Groups sobre a VPC default. Não criamos VPC/NAT/EIP próprios de
# propósito: NAT Gateway e Elastic IP ocioso geram custo fora do free tier.

variable "project" { type = string }
variable "env" { type = string }
variable "vpc_id" { type = string }

# SG do app (instância do Elastic Beanstalk). Entrada HTTP pública no MVP;
# na frente entrará o CloudFront (E7) e podemos restringir a origem depois.
resource "aws_security_group" "app" {
  name        = "${var.project}-${var.env}-app"
  description = "App (Elastic Beanstalk) - trafego HTTP de entrada"
  vpc_id      = var.vpc_id

  ingress {
    description = "HTTP"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "Saida liberada"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-${var.env}-app" }
}

# SG do banco: só aceita 5432 vindo do SG do app. Sem exposição pública.
resource "aws_security_group" "db" {
  name        = "${var.project}-${var.env}-db"
  description = "RDS PostgreSQL - acesso apenas a partir do app"
  vpc_id      = var.vpc_id

  ingress {
    description     = "PostgreSQL a partir do app"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.app.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-${var.env}-db" }
}

output "app_sg_id" { value = aws_security_group.app.id }
output "db_sg_id" { value = aws_security_group.db.id }
