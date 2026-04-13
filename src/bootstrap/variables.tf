variable "argocd_ui_host" {
  description = "Host or IP where the ArgoCD NodePort service is reachable"
  type        = string
  sensitive   = true
}
