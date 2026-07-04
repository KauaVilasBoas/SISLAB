# Bucket S3 privado para anexos do sistema (comprovantes de entrada de estoque,
# fichas de equipamento etc.). Acesso apenas pela aplicação via credencial IAM;
# uploads/downloads pelo usuário serão feitos por URL pré-assinada (E8/E3).

variable "project" { type = string }
variable "env" { type = string }

resource "aws_s3_bucket" "attachments" {
  bucket = "${var.project}-${var.env}-attachments"
  tags   = { Name = "${var.project}-${var.env}-attachments" }
}

resource "aws_s3_bucket_versioning" "attachments" {
  bucket = aws_s3_bucket.attachments.id
  versioning_configuration { status = "Enabled" }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "attachments" {
  bucket = aws_s3_bucket.attachments.id
  rule {
    apply_server_side_encryption_by_default { sse_algorithm = "AES256" }
  }
}

resource "aws_s3_bucket_public_access_block" "attachments" {
  bucket                  = aws_s3_bucket.attachments.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# CORS para permitir upload/download direto do SPA via URL pré-assinada.
resource "aws_s3_bucket_cors_configuration" "attachments" {
  bucket = aws_s3_bucket.attachments.id
  cors_rule {
    allowed_methods = ["GET", "PUT", "POST", "HEAD"]
    allowed_origins = ["*"] # refinar para o domínio do SPA no E7
    allowed_headers = ["*"]
    max_age_seconds = 3000
  }
}

output "bucket_name" { value = aws_s3_bucket.attachments.id }
output "bucket_arn" { value = aws_s3_bucket.attachments.arn }
