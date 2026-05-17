variable "project_name" {
  description = "Nome base usado nos recursos AWS."
  type        = string
  default     = "fase4-games-api"
}

variable "environment" {
  description = "Ambiente da implantacao."
  type        = string
  default     = "dev"
}

variable "aws_region" {
  description = "Regiao AWS onde a infraestrutura sera criada."
  type        = string
  default     = "us-east-1"
}

variable "cluster_name" {
  description = "Nome do cluster EKS."
  type        = string
  default     = "fcg-fase4"
}

variable "vpc_cidr" {
  description = "CIDR da VPC."
  type        = string
  default     = "10.50.0.0/16"
}

variable "cluster_version" {
  description = "Versao do Kubernetes no EKS."
  type        = string
  default     = "1.30"
}

variable "node_instance_types" {
  description = "Tipos de instancias EC2 para o managed node group."
  type        = list(string)
  default     = ["m7i-flex.large"]
}

variable "node_min_size" {
  description = "Quantidade minima de nos no node group."
  type        = number
  default     = 2
}

variable "node_max_size" {
  description = "Quantidade maxima de nos no node group."
  type        = number
  default     = 4
}

variable "node_desired_size" {
  description = "Quantidade desejada de nos no node group."
  type        = number
  default     = 2
}

variable "games_table_name" {
  description = "Nome da tabela DynamoDB usada pela Games API."
  type        = string
  default     = "Games"
}

variable "kubernetes_namespace" {
  description = "Namespace Kubernetes onde a Games API sera publicada."
  type        = string
  default     = "fase4"
}

variable "games_api_service_account" {
  description = "ServiceAccount Kubernetes usada pela Games API para IRSA."
  type        = string
  default     = "games-api"
}
