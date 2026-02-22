# homelab

My homelab setup all in one repository. :)

## Tech Stack

## Development

´´´bash
talosctl get disks -n 10.0.0.220 --insecure # list disks of the node - nice for finding out which disk is the one you want to install Talos on

terraform output -raw kubeconfig > ~/.kube/config # export kubeconfig from terraform output
chmod 600 ~/.kube/config # set permissions for kubeconfig
kubectl get nodes # verify cluster access

mkdir -p ~/.talos
talosctl dashboard -n 10.0.0.220 # when endpoint for talosctl is defined in config

talosctl config info

# getting hardware info of a node
talosctl get systemInformation -n 10.0.0.220

# getting MAC address of a node
talosctl get links -n 10.0.0.220

# editing the machine configuration manually (sadly not possible to adjust the hostname on this way; for this you have to connect directly with the node and edit network config there)
talosctl -n 10.0.0.228 edit mc --mode=staged

talosctl get members
´´´

## References

https://docs.siderolabs.com/talos/v1.9/platform-specific-installations/bare-metal-platforms/iso

https://www.ventoy.net/en/download.html

https://registry.terraform.io/providers/siderolabs/talos/latest/docs

https://docs.siderolabs.com/talos/v1.12/getting-started/talosctl#alternative-install
