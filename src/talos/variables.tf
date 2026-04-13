variable "controlplane_node_ip" {
  description = "Control plane node IP address used by Talos provider operations"
  type        = string
  sensitive   = true
}

variable "cluster_endpoint" {
  description = "Optional Kubernetes API endpoint. If null, derived from controlplane_node_ip"
  type        = string
  default     = null
  nullable    = true
  sensitive   = true
}

variable "controlplane_install_disk" {
  description = "Target disk path for Talos installation on the control plane node"
  type        = string
  default     = "/dev/sda"
}

variable "controlplane_hostname" {
  description = "Hostname of the control plane node"
  type        = string
  default     = "hl-controlplane"
}

variable "workers" {
  description = "Worker nodes keyed by logical name"
  type = map(object({
    ip   = string
    disk = string
  }))
  sensitive = true
}
