output "ecr_repository_url" {
  description = "ECR repository the deploy workflow pushes to."
  value       = aws_ecr_repository.api.repository_url
}

output "ecs_cluster" {
  description = "ECS cluster name."
  value       = aws_ecs_cluster.this.name
}

output "ecs_service" {
  description = "ECS service name."
  value       = aws_ecs_service.api.name
}

output "task_family" {
  description = "ECS task-definition family."
  value       = aws_ecs_task_definition.api.family
}

output "api_url" {
  description = "Public URL of the API (via the load balancer)."
  value       = "http://${aws_lb.api.dns_name}"
}
