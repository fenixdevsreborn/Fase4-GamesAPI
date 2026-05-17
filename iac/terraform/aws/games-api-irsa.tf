data "aws_iam_policy_document" "games_api_assume_role" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    effect  = "Allow"

    principals {
      type        = "Federated"
      identifiers = [module.eks.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${replace(module.eks.cluster_oidc_issuer_url, "https://", "")}:sub"
      values   = ["system:serviceaccount:${var.kubernetes_namespace}:${var.games_api_service_account}"]
    }

    condition {
      test     = "StringEquals"
      variable = "${replace(module.eks.cluster_oidc_issuer_url, "https://", "")}:aud"
      values   = ["sts.amazonaws.com"]
    }
  }
}

data "aws_iam_policy_document" "games_api_dynamodb" {
  statement {
    effect = "Allow"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:UpdateItem",
      "dynamodb:DeleteItem",
      "dynamodb:Scan",
      "dynamodb:Query",
      "dynamodb:DescribeTable"
    ]
    resources = [
      aws_dynamodb_table.games.arn,
      "${aws_dynamodb_table.games.arn}/index/*"
    ]
  }
}

resource "aws_iam_role" "games_api" {
  name               = "${local.name}-games-api"
  assume_role_policy = data.aws_iam_policy_document.games_api_assume_role.json
}

resource "aws_iam_policy" "games_api_dynamodb" {
  name        = "${local.name}-games-api-dynamodb"
  description = "Permissoes da Games API para acessar a tabela DynamoDB."
  policy      = data.aws_iam_policy_document.games_api_dynamodb.json
}

resource "aws_iam_role_policy_attachment" "games_api_dynamodb" {
  role       = aws_iam_role.games_api.name
  policy_arn = aws_iam_policy.games_api_dynamodb.arn
}
