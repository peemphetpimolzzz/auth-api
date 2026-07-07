// auth-api — AWS infrastructure (deployment-ready).
//
// Runs the ASP.NET Core API on ECS Fargate behind an Application Load Balancer,
// backed by an RDS PostgreSQL instance. The image lives in ECR. The DB
// connection string and JWT signing key are stored in Secrets Manager and
// injected into the task at runtime (never baked into the image or task JSON).
//
// Deploys into the account's default VPC to keep the template self-contained.
// CI/CD authenticates with GitHub OIDC (role setup documented in
// docs/deployment.md) — no long-lived AWS keys are stored in the repository.

locals {
  name = var.app_name
  tags = {
    Project   = var.app_name
    ManagedBy = "terraform"
  }
}

data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

// ---------------------------------------------------------------------------
// Container registry
// ---------------------------------------------------------------------------
resource "aws_ecr_repository" "api" {
  name                 = local.name
  image_tag_mutability = "MUTABLE"
  force_delete         = true

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = local.tags
}

// ---------------------------------------------------------------------------
// Security groups
// ---------------------------------------------------------------------------
resource "aws_security_group" "alb" {
  name        = "${local.name}-alb"
  description = "Ingress to the load balancer"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description = "HTTP"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.tags
}

resource "aws_security_group" "service" {
  name        = "${local.name}-service"
  description = "ECS tasks — reachable only from the ALB"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description     = "API port from ALB"
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.tags
}

resource "aws_security_group" "db" {
  name        = "${local.name}-db"
  description = "PostgreSQL — reachable only from the ECS tasks"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description     = "PostgreSQL from service"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.service.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.tags
}

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
resource "aws_db_subnet_group" "db" {
  name       = "${local.name}-db"
  subnet_ids = data.aws_subnets.default.ids
  tags       = local.tags
}

resource "aws_db_instance" "postgres" {
  identifier = "${local.name}-db"
  engine     = "postgres"
  # Major-version only — RDS selects a currently-supported minor, avoiding a
  # pinned minor that becomes uncreatable once AWS retires it.
  engine_version         = "17"
  instance_class         = "db.t4g.micro"
  allocated_storage      = 20
  storage_type           = "gp3"
  db_name                = var.db_name
  username               = var.db_username
  password               = var.db_password
  db_subnet_group_name   = aws_db_subnet_group.db.name
  vpc_security_group_ids = [aws_security_group.db.id]
  publicly_accessible    = false
  skip_final_snapshot    = true
  deletion_protection    = false
  storage_encrypted      = true
  apply_immediately      = true

  tags = local.tags
}

// ---------------------------------------------------------------------------
// Secrets (connection string + JWT key)
// ---------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "conn" {
  name = "${local.name}/connection-string"
  # Delete immediately on destroy so a re-apply is not blocked by the default
  # 30-day recovery window retaining the name.
  recovery_window_in_days = 0
  tags                    = local.tags
}

resource "aws_secretsmanager_secret_version" "conn" {
  secret_id     = aws_secretsmanager_secret.conn.id
  secret_string = "Host=${aws_db_instance.postgres.address};Port=5432;Database=${var.db_name};Username=${var.db_username};Password=${var.db_password}"
}

resource "aws_secretsmanager_secret" "jwt" {
  name                    = "${local.name}/jwt-key"
  recovery_window_in_days = 0
  tags                    = local.tags
}

resource "aws_secretsmanager_secret_version" "jwt" {
  secret_id     = aws_secretsmanager_secret.jwt.id
  secret_string = var.jwt_key
}

// ---------------------------------------------------------------------------
// IAM roles for ECS
// ---------------------------------------------------------------------------
data "aws_iam_policy_document" "ecs_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "execution" {
  name               = "${local.name}-exec"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume.json
  tags               = local.tags
}

resource "aws_iam_role_policy_attachment" "execution_managed" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "read_secrets" {
  statement {
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [aws_secretsmanager_secret.conn.arn, aws_secretsmanager_secret.jwt.arn]
  }
}

resource "aws_iam_role_policy" "execution_secrets" {
  name   = "${local.name}-read-secrets"
  role   = aws_iam_role.execution.id
  policy = data.aws_iam_policy_document.read_secrets.json
}

resource "aws_iam_role" "task" {
  name               = "${local.name}-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume.json
  tags               = local.tags
}

// ---------------------------------------------------------------------------
// ECS cluster, task definition, service
// ---------------------------------------------------------------------------
resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${local.name}"
  retention_in_days = 30
  tags              = local.tags
}

resource "aws_ecs_cluster" "this" {
  name = local.name
  tags = local.tags
}

resource "aws_ecs_task_definition" "api" {
  family                   = local.name
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.container_image
      essential = true
      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]
      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "Jwt__Issuer", value = var.jwt_issuer },
        { name = "Jwt__Audience", value = var.jwt_audience }
      ]
      secrets = [
        { name = "ConnectionStrings__Default", valueFrom = aws_secretsmanager_secret.conn.arn },
        { name = "Jwt__Key", valueFrom = aws_secretsmanager_secret.jwt.arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "api"
        }
      }
    }
  ])

  tags = local.tags
}

resource "aws_lb" "api" {
  name               = local.name
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = data.aws_subnets.default.ids
  tags               = local.tags
}

resource "aws_lb_target_group" "api" {
  name        = local.name
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = data.aws_vpc.default.id
  target_type = "ip"

  health_check {
    path                = "/health"
    matcher             = "200"
    interval            = 30
    healthy_threshold   = 2
    unhealthy_threshold = 5
  }

  tags = local.tags
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.api.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    # With a certificate configured, redirect all HTTP to HTTPS; otherwise
    # forward (demo default — see the certificate_arn variable).
    type             = var.certificate_arn == "" ? "forward" : "redirect"
    target_group_arn = var.certificate_arn == "" ? aws_lb_target_group.api.arn : null

    dynamic "redirect" {
      for_each = var.certificate_arn == "" ? [] : [1]
      content {
        port        = "443"
        protocol    = "HTTPS"
        status_code = "HTTP_301"
      }
    }
  }
}

# HTTPS listener — created only when an ACM certificate ARN is supplied, so the
# API can be served over TLS (required before it handles real credentials/JWTs).
resource "aws_lb_listener" "https" {
  count             = var.certificate_arn == "" ? 0 : 1
  load_balancer_arn = aws_lb.api.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

resource "aws_ecs_service" "api" {
  name            = local.name
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = data.aws_subnets.default.ids
    security_groups  = [aws_security_group.service.id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }

  // The deploy workflow registers new task-definition revisions, so ignore
  // image drift here to avoid Terraform reverting a rollout.
  lifecycle {
    ignore_changes = [task_definition]
  }

  # Wait for the execution role's permissions before ECS schedules a task, so
  # the first task can pull the image and read secrets (IAM is eventually
  # consistent, so ordering alone is not enough without these edges).
  depends_on = [
    aws_lb_listener.http,
    aws_iam_role_policy_attachment.execution_managed,
    aws_iam_role_policy.execution_secrets,
  ]

  tags = local.tags
}
