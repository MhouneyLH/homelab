# homelab
My homelab setup all in one repository. :)

## Tech Stack



## Development

´´´bash
talosctl get disks -n 10.0.0.220 --insecure # list disks of the node - nice for finding out which disk is the one you want to install Talos on

mkdir -p ~/.talos
terraform output -raw talosconfig > ~/.talos/config

talosctl dashboard -n 10.0.0.220 -e 10.0.0.220 # when not having defined endpoint in the config, you can specify it here explicitly

talosctl dashboard -n 10.0.0.220 # when endpoint for talosctl is defined in config

talosctl config info

# getting hardware info of a node
talosctl get hardware -n 10.0.0.220
´´´

## References

https://docs.siderolabs.com/talos/v1.9/platform-specific-installations/bare-metal-platforms/iso

https://www.ventoy.net/en/download.html

https://registry.terraform.io/providers/siderolabs/talos/latest/docs

https://docs.siderolabs.com/talos/v1.12/getting-started/talosctl#alternative-install