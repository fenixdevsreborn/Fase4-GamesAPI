param(
  [string]$Namespace = "fase4",
  [string]$Image = "adinteltidev/games-api:latest"
)

$ErrorActionPreference = "Stop"
function Require-Env([string]$Name) {
  $value = [Environment]::GetEnvironmentVariable($Name)
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "Environment variable '$Name' is required to create Kubernetes secrets."
  }
  return $value
}

function Apply-Secret([string]$Name, [string[]]$Literals) {
  kubectl create secret generic $Name -n $Namespace @Literals --dry-run=client -o yaml | kubectl apply -f -
}

docker build -t $Image .

kubectl apply -f k8s/local/00-namespace.yaml
Apply-Secret "games-api-secrets" @(
  "--from-literal=jwt-secret=$(Require-Env 'JWT_SECRET')",
  "--from-literal=jwt-issuer=$(Require-Env 'JWT_ISSUER')",
  "--from-literal=jwt-audience=$(Require-Env 'JWT_AUDIENCE')",
  "--from-literal=jwt-key-id=$(Require-Env 'JWT_KEY_ID')",
  "--from-literal=aws-access-key-id=$(Require-Env 'LOCAL_AWS_ACCESS_KEY_ID')",
  "--from-literal=aws-secret-access-key=$(Require-Env 'LOCAL_AWS_SECRET_ACCESS_KEY')"
)
Apply-Secret "rabbitmq-secrets" @(
  "--from-literal=rabbitmq-user=$(Require-Env 'RABBITMQ_USERNAME')",
  "--from-literal=rabbitmq-pass=$(Require-Env 'RABBITMQ_PASSWORD')",
  "--from-literal=rabbitmq-vhost=$(Require-Env 'RABBITMQ_VHOST')"
)
kubectl apply -f k8s/local/02-rabbitmq.yaml
kubectl apply -f k8s/local/03-dynamodb-local.yaml
kubectl apply -f k8s/local/03-dynamodb-init-job.yaml
kubectl apply -f k8s/local/03-redis.yaml
kubectl apply -f k8s/local/03-elasticsearch.yaml
kubectl apply -f k8s/local/04-games-api.yaml
kubectl apply -f k8s/local/05-service.yaml

kubectl rollout status deployment/redis -n $Namespace
kubectl rollout status deployment/elasticsearch -n $Namespace
kubectl rollout status deployment/games-api -n $Namespace
kubectl get pods -n $Namespace
