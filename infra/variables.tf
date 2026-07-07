variable "region" {
  description = "AWS region."
  type        = string
  default     = "ap-southeast-1"
}

variable "app_name" {
  description = "Base name used to derive resource names."
  type        = string
  default     = "auth-api"
}

variable "container_image" {
  description = "Container image for the API task. Defaults to a public placeholder for the first infra-only apply; the deploy workflow registers a new task definition pointing at the ECR image on each release."
  type        = string
  default     = "public.ecr.aws/nginx/nginx:stable"
}

variable "db_username" {
  description = "PostgreSQL master username."
  type        = string
  default     = "authapi"
}

variable "db_password" {
  description = "PostgreSQL master password. Supply at apply time via TF_VAR_db_password — do not commit."
  type        = string
  sensitive   = true

  # Restrict to characters that are safe both in the Npgsql key=value
  # connection string (no ';' or '=') and in a URL — a stray delimiter in the
  # password would otherwise corrupt the connection string and break startup.
  validation {
    condition     = can(regex("^[A-Za-z0-9!*_.~-]{8,}$", var.db_password))
    error_message = "db_password must be 8+ chars using only letters, digits, and ! * _ . ~ - (characters that are safe inside the connection string)."
  }
}

variable "db_name" {
  description = "PostgreSQL database name."
  type        = string
  default     = "authapi"
}

variable "jwt_key" {
  description = "HS256 signing key (>= 32 chars). Supply at apply time via TF_VAR_jwt_key — do not commit."
  type        = string
  sensitive   = true

  # HS256 requires a 256-bit (32-byte) key; a shorter key applies cleanly but
  # crashes the app at boot, so reject it here where the error is actionable.
  validation {
    condition     = length(var.jwt_key) >= 32
    error_message = "jwt_key must be at least 32 characters (256 bits) for HS256."
  }
}

variable "certificate_arn" {
  description = "ACM certificate ARN for the ALB. When set, an HTTPS:443 listener is added and HTTP:80 redirects to it; when empty, the ALB serves HTTP:80 only (suitable only for demos / non-sensitive traffic)."
  type        = string
  default     = ""
}

variable "jwt_issuer" {
  description = "JWT issuer claim."
  type        = string
  default     = "auth-api"
}

variable "jwt_audience" {
  description = "JWT audience claim."
  type        = string
  default     = "auth-api-clients"
}

variable "desired_count" {
  description = "Number of ECS tasks to run."
  type        = number
  default     = 1
}
