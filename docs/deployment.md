# Deploying to AWS

The API runs on **ECS Fargate** behind an **Application Load Balancer**, backed by an
**RDS PostgreSQL** instance. Images live in **Amazon ECR**. The database connection string
and the JWT signing key are stored in **Secrets Manager** and injected into the task at
runtime — never baked into the image or the task definition. CI/CD authenticates with
**GitHub OIDC** (assume-role), so no long-lived AWS keys are stored in the repository.

```
GitHub Actions ──OIDC assume-role──▶ AWS
      │
      ├─ docker build / push ─────▶ ECR
      │
      └─ ecs deploy ──▶ ALB (:80) ──▶ ECS Fargate task (API :8080 /health)
                                              │  secrets: conn string, JWT key
                                              ▼
                                      RDS PostgreSQL
```

> Everything here is **deployment-ready** infrastructure-as-code. It has not been applied
> to a live AWS account in this repository — follow the steps below to provision it in your
> own account.

The Terraform deploys into the account's **default VPC** to stay self-contained.

## Prerequisites

- Docker and git (Terraform and the AWS CLI run from containers — no host install needed).
- An AWS account and credentials with permission to create the resources below.

Run Terraform and the AWS CLI without installing them:

```bash
tf()  { docker run --rm -it -v "$PWD:/w" -w /w/infra -e AWS_PROFILE -v "$HOME/.aws:/root/.aws" hashicorp/terraform:latest "$@"; }
aws() { docker run --rm -it -v "$HOME/.aws:/root/.aws" -e AWS_PROFILE amazon/aws-cli:latest "$@"; }
```

## 1. Provision the infrastructure

```bash
export TF_VAR_db_password='<a-strong-password>'
export TF_VAR_jwt_key='<a-32+-char-signing-key>'

tf init
tf apply
```

Terraform outputs `ecr_repository_url`, `ecs_cluster`, `ecs_service`, `task_family`, and
`api_url`. On the first apply the service starts from a public placeholder image; the deploy
workflow replaces it with the real image below.

## 2. Create the GitHub OIDC role

If the account does not already have the GitHub OIDC provider, create it once:

```bash
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1
```

Create a deploy role whose trust policy is scoped to this repository, and attach a policy
allowing ECR push, ECS update, and `iam:PassRole` for the task/execution roles. See the
[AWS docs on configuring OIDC](https://docs.github.com/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-amazon-web-services)
for a ready-made trust policy (subject `repo:peemphetpimolzzz/auth-api:ref:refs/heads/main`).

## 3. Configure the repository

**Settings → Secrets and variables → Actions**

| Kind | Name | Value |
|------|------|-------|
| Secret | `AWS_DEPLOY_ROLE_ARN` | ARN of the deploy role from step 2 |
| Variable | `AWS_REGION` | e.g. `ap-southeast-1` |
| Variable | `ECR_REPOSITORY` | `auth-api` (repo name from `ecr_repository_url`) |
| Variable | `ECS_CLUSTER` | `ecs_cluster` output |
| Variable | `ECS_SERVICE` | `ecs_service` output |
| Variable | `TASK_FAMILY` | `task_family` output |

## 4. Deploy

Run the **Deploy (AWS)** workflow from the Actions tab (it is `workflow_dispatch` only until
the secrets above are set; re-enable the `push` trigger in `deploy.yml` to deploy on every
merge to `main`). It builds the image, pushes it to ECR, registers a new task-definition
revision, and waits for the ECS service to stabilise. Verify against the load-balancer URL:

```bash
curl -f "$(tf output -raw api_url)/health"
```

## Teardown

```bash
tf destroy
```
