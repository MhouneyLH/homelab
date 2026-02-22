terraform {
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.35"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.17"
    }
  }
}

provider "kubernetes" {
  config_path = pathexpand("~/.kube/config")
}

provider "helm" {
  kubernetes {
    config_path = pathexpand("~/.kube/config")
  }
}

# Create argocd namespace
resource "kubernetes_namespace" "argocd" {
  metadata {
    name = "argocd"
  }
}

# Install ArgoCD via Helm
resource "helm_release" "argocd" {
  name       = "argocd"
  repository = "https://argoproj.github.io/argo-helm"
  chart      = "argo-cd"
  namespace  = kubernetes_namespace.argocd.metadata[0].name
  version    = "9.4.3"

  values = [
    file("${path.module}/argocd-values.yaml")
  ]

  depends_on = [kubernetes_namespace.argocd]
}

output "argocd_server_admin_password" {
  description = "Initial admin password for ArgoCD server. Default username is 'admin'."
  value       = "Run: kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d && echo"
}

# found out the port: kubectl get svc argocd-server -n argocd
# deployed it intentionally on control plane using the node selector in the argocd-values.yaml
output "argocd_ui_url" {
  description = "URL to access ArgoCD UI"
  value       = "http://10.0.0.220:30080"
}
