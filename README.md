# Games API - Fase 3

API .NET 8 para catalogo de jogos e solicitacao de compras. A aplicacao foi migrada de AWS Lambda para uma Web API containerizada, pronta para execucao em Docker, Kubernetes local e Amazon EKS.

## Arquitetura

- .NET 8 Web API executando via Kestrel na porta `8080`.
- Imagem Docker publicada no Docker Hub por GitHub Actions.
- Deploy em Kubernetes local ou Amazon EKS.
- Amazon DynamoDB mantido como banco NoSQL da API.
- RabbitMQ substitui o antigo fluxo baseado em Amazon SQS.
- Autenticacao JWT Bearer usando o token emitido pela `ms-usersapi`.
- Terraform cria VPC, EKS, AWS Load Balancer Controller, Metrics Server, DynamoDB e IAM Role IRSA.

## Configuracoes principais

Variaveis esperadas pela aplicacao:

- `AWS__Region`
- `AWS__ServiceURL`, apenas para DynamoDB Local
- `DynamoDb__GamesTable`
- `Jwt__Secret`
- `Jwt__Issuer`
- `Jwt__Audience`
- `RabbitMq__Host`
- `RabbitMq__Port`
- `RabbitMq__Username`
- `RabbitMq__Password`
- `RabbitMq__VirtualHost`
- `RabbitMq__ExchangeName`
- `RabbitMq__PaymentQueueName`

Os valores de JWT devem ser compativeis com a `ms-usersapi`, especialmente `Jwt__Secret`, `Jwt__Issuer` e `Jwt__Audience`.

## Execucao local com Docker Compose

```powershell
docker compose up --build
```

Servicos locais:

- Games API: `http://localhost:5001`
- RabbitMQ Management: `http://localhost:15672`
- DynamoDB Local: `http://localhost:8000`

O Compose cria a tabela DynamoDB `Games` automaticamente no DynamoDB Local. Para rodar a Games API junto com a Users API usando um unico RabbitMQ, execute o Compose da raiz do workspace:

```powershell
cd ..
docker compose up --build
```

Esse arquivo sobe um unico `fiap-rabbitmq`, com vhost `fiap`, exchange `fiap.events`, Users API em `http://localhost:5000` e Games API em `http://localhost:5001`.

## Execucao local com Kubernetes

```powershell
.\deployLocal.ps1
```

Ou manualmente:

```powershell
kubectl apply -f k8s/local
kubectl rollout status deployment/games-api -n fase4
```

## Infraestrutura AWS

```powershell
.\criarClusterEks.ps1
```

Ou manualmente:

```powershell
cd iac/terraform/aws
Copy-Item terraform.tfvars.example terraform.tfvars
terraform init
terraform plan
terraform apply
```

Depois do `apply`, use o output `games_api_role_arn` na ServiceAccount Kubernetes da Games API.

## Deploy no EKS

```powershell
.\deployEks.ps1 `
  -ClusterName fase4-games-api-dev `
  -Region us-east-1 `
  -Image adinteltidev/games-api:latest `
  -GamesApiRoleArn <role-arn-gerado-pelo-terraform>
```

## CI/CD

Workflows:

- `.github/workflows/docker-build-push.yml`: build e push da imagem para Docker Hub.
- `.github/workflows/deploy-eks.yml`: aplica manifests e atualiza a imagem no EKS.

Secrets esperados:

- `DOCKER_HUB_USERNAME`
- `DOCKER_HUB_TOKEN`
- `DOCKER_HUB_REPOSITORY`
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_REGION`
- `EKS_CLUSTER_NAME`
- `GAMES_API_ROLE_ARN`
