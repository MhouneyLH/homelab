locals {
  node_ip            = var.controlplane_node_ip
  cluster_endpoint   = var.cluster_endpoint != null ? var.cluster_endpoint : "https://${var.controlplane_node_ip}:6443"
  talos_install_disk = var.controlplane_install_disk
  hostname           = var.controlplane_hostname
}

resource "talos_machine_secrets" "controlplane" {

}

data "talos_machine_configuration" "controlplane" {
  cluster_name     = local.cluster_name
  machine_type     = "controlplane"
  cluster_endpoint = local.cluster_endpoint # k8s api server endpoint; kubelet, kube-proxy and other components use this to talk to controlplane
  machine_secrets  = talos_machine_secrets.controlplane.machine_secrets
}

data "talos_client_configuration" "controlplane" {
  cluster_name         = local.cluster_name
  client_configuration = talos_machine_secrets.controlplane.client_configuration
  nodes                = [local.node_ip]
  endpoints            = [local.node_ip] # this is the actual API endpoint of the cluster; used by talosctl to talk to cluster
}

resource "talos_machine_configuration_apply" "controlplane" {
  client_configuration        = talos_machine_secrets.controlplane.client_configuration
  machine_configuration_input = data.talos_machine_configuration.controlplane.machine_configuration
  node                        = local.node_ip
  config_patches = [
    yamlencode({
      machine = {
        install = {
          disk = local.talos_install_disk
        }
        # network = {
        #   hostname = local.hostname
        # }
      }
    })
  ]
}

# initialzing other k8s components like etcd
resource "talos_machine_bootstrap" "controlplane" {
  client_configuration = talos_machine_secrets.controlplane.client_configuration
  node                 = local.node_ip
  depends_on           = [talos_machine_configuration_apply.controlplane]
}

# for reading out the generated config for being able to use it with talosctl locally
output "talosconfig" {
  value     = data.talos_client_configuration.controlplane.talos_config
  sensitive = true
}

# kubeconfig for accessing the kubernetes cluster
data "talos_cluster_kubeconfig" "controlplane" {
  client_configuration = talos_machine_secrets.controlplane.client_configuration
  node                 = local.node_ip
  depends_on           = [talos_machine_bootstrap.controlplane]
}

output "kubeconfig" {
  value     = data.talos_cluster_kubeconfig.controlplane.kubeconfig_raw
  sensitive = true
}
