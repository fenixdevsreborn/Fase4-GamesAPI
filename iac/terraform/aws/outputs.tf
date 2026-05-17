output "cluster_name" {
  description = "Nome do cluster EKS."
  value       = module.eks.cluster_name
}

output "cluster_endpoint" {
  description = "Endpoint do cluster EKS."
  value       = module.eks.cluster_endpoint
}

output "configure_kubectl_command" {
  description = "Comando para configurar o kubectl local."
  value       = "aws eks update-kubeconfig --region ${var.aws_region} --name ${module.eks.cluster_name}"
}

output "vpc_id" {
  description = "ID da VPC criada."
  value       = module.vpc.vpc_id
}

output "games_table_name" {
  description = "Nome da tabela DynamoDB criada."
  value       = aws_dynamodb_table.games.name
}

output "games_api_role_arn" {
  description = "ARN da IAM Role para anotar a ServiceAccount da Games API."
  value       = aws_iam_role.games_api.arn
}
