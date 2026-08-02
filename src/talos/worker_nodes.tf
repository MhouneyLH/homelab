data "talos_machine_configuration" "worker" {
  for_each = var.workers

  cluster_name     = local.cluster_name
  machine_type     = "worker"
  cluster_endpoint = local.cluster_endpoint
  machine_secrets  = talos_machine_secrets.controlplane.machine_secrets
}

resource "talos_machine_configuration_apply" "worker" {
  for_each = var.workers

  client_configuration        = talos_machine_secrets.controlplane.client_configuration
  machine_configuration_input = data.talos_machine_configuration.worker[each.key].machine_configuration
  node                        = each.value.ip

  config_patches = [
    yamlencode({
      machine = {
        install = {
          disk = each.value.disk
        }
        # Internal Harbor registry (see src/k8s/apps/platform/harbor) is plain HTTP - no TLS,
        # since it's LAN-internal only. containerd refuses non-TLS registries by default, so
        # this tells it to treat this specific endpoint as HTTP rather than attempting HTTPS.
        registries = {
          mirrors = {
            "${each.value.ip}:30002" = {
              endpoints = ["http://${each.value.ip}:30002"]
            }
          }
        }
      }
    })
  ]
}
