locals {
  workers = {
    "hl-worker-01" = { # elitedesk
      ip   = "10.0.0.228"
      disk = "/dev/nvme0n1"
    }
  }
}

data "talos_machine_configuration" "worker" {
  for_each = local.workers

  cluster_name     = local.cluster_name
  machine_type     = "worker"
  cluster_endpoint = local.cluster_endpoint
  machine_secrets  = talos_machine_secrets.controlplane.machine_secrets
}

resource "talos_machine_configuration_apply" "worker" {
  for_each = local.workers

  client_configuration        = talos_machine_secrets.controlplane.client_configuration
  machine_configuration_input = data.talos_machine_configuration.worker[each.key].machine_configuration
  node                        = each.value.ip

  config_patches = [
    yamlencode({
      machine = {
        install = {
          disk = each.value.disk
        }
      }
    })
  ]
}
