param(
  [string]$TerraformPath = "iac/terraform/aws"
)

$ErrorActionPreference = "Stop"

Push-Location $TerraformPath
try {
  if (-not (Test-Path "terraform.tfvars")) {
    Copy-Item "terraform.tfvars.example" "terraform.tfvars"
  }

  terraform init
  terraform plan
  terraform apply
}
finally {
  Pop-Location
}
