param(
  [string]$Namespace = "fase4",
  [string]$Region = "us-east-1",
  [string]$ClusterName = "fase4-games-api-dev",
  [string]$Image = "adinteltidev/games-api:latest",
  [string]$GamesApiRoleArn = ""
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

aws eks update-kubeconfig --name $ClusterName --region $Region

kubectl apply -f k8s/eks/00-namespace.yaml
Apply-Secret "games-api-secrets" @(
  "--from-literal=jwt-secret=$(Require-Env 'JWT_SECRET')",
  "--from-literal=jwt-issuer=$(Require-Env 'JWT_ISSUER')",
  "--from-literal=jwt-audience=$(Require-Env 'JWT_AUDIENCE')",
  "--from-literal=jwt-key-id=$(Require-Env 'JWT_KEY_ID')"
)
Apply-Secret "rabbitmq-secrets" @(
  "--from-literal=rabbitmq-user=$(Require-Env 'RABBITMQ_USERNAME')",
  "--from-literal=rabbitmq-pass=$(Require-Env 'RABBITMQ_PASSWORD')",
  "--from-literal=rabbitmq-vhost=$(Require-Env 'RABBITMQ_VHOST')"
)
kubectl apply -f k8s/eks/02-serviceaccount.yaml

if ($GamesApiRoleArn -ne "") {
  kubectl annotate serviceaccount games-api `
    eks.amazonaws.com/role-arn=$GamesApiRoleArn `
    -n $Namespace `
    --overwrite
}

kubectl apply -f k8s/eks/03-rabbitmq.yaml
kubectl apply -f k8s/eks/03-redis.yaml
kubectl apply -f k8s/eks/03-elasticsearch.yaml
kubectl apply -f k8s/eks/04-games-api.yaml
kubectl apply -f k8s/eks/05-service.yaml
kubectl apply -f k8s/eks/06-hpa.yaml
kubectl apply -f k8s/eks/07-ingress.yaml

kubectl set image deployment/games-api games-api=$Image -n $Namespace
kubectl rollout status deployment/redis -n $Namespace
kubectl rollout status deployment/elasticsearch -n $Namespace
kubectl rollout status deployment/games-api -n $Namespace
kubectl get ingress -n $Namespace
