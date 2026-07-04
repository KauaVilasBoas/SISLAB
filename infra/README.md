# SISLAB — Infraestrutura (Terraform)

IaC da infraestrutura AWS do SISLAB. Região padrão: **sa-east-1**. Tudo dimensionado
para o **AWS Free Tier** (1x EC2 t3.micro, RDS db.t3.micro single-AZ, S3, sem load
balancer/NAT). Um **AWS Budget** com alerta por e-mail protege contra custo inesperado.

## Estrutura

```
infra/
├── bootstrap/       # state remoto (bucket S3) + AWS Budget — state LOCAL, aplica 1x
├── envs/staging/    # ambiente staging (backend S3) — chama os módulos
└── modules/
    ├── network/     # security groups sobre a VPC default
    ├── database/    # RDS PostgreSQL + segredos no SSM Parameter Store
    ├── compute/     # Elastic Beanstalk SingleInstance (.NET 8) + IAM roles
    └── storage/     # bucket S3 de anexos
```

## Pré-requisitos
- AWS CLI v2 configurada (`aws configure`, região sa-east-1) — ver credenciais do projeto.
- Terraform >= 1.11 (usamos lock nativo de state no S3, sem DynamoDB).

## Ordem de execução

### 1) Bootstrap (uma vez por conta)
Cria o bucket de state remoto e o orçamento de billing. Usa state local.
```
cd infra/bootstrap
terraform init
terraform apply
```
Anote o output `state_bucket_name` (ex.: `sislab-tfstate-123456789012`).

### 2) Ambiente staging
```
cd infra/envs/staging
terraform init -backend-config="bucket=<state_bucket_name do passo 1>"
terraform plan      # revisar
terraform apply
```

Outputs relevantes: `api_url` (Elastic Beanstalk), `db_endpoint` (RDS privado),
`attachments_bucket`, `ssm_prefix`.

## Notas de segurança / custo
- Segredos (senha do banco, connection string) são gerados pelo Terraform e gravados
  no **SSM Parameter Store como SecureString** — nunca em texto claro no repo. O state
  do Terraform contém valores sensíveis: por isso fica **cifrado no S3** e o `.tfstate`
  está no `.gitignore`.
- O app lê a connection string do SSM em runtime (a instância EB tem permissão mínima
  de leitura no prefixo `/sislab/staging/*`).
- `skip_final_snapshot = true` e `deletion_protection = false` no RDS são adequados para
  **staging**; ao criar o ambiente de produção, inverta os dois.
- Deploy da aplicação (bundle .NET no Beanstalk) é feito pela pipeline (card [E8] GitHub
  Actions), não pelo Terraform — o Terraform provisiona a plataforma, não publica o código.
