#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../.env"

IFS=',' read -r -a NODES <<< "$HOMELAB_NODE_IP_ADDRESSES"

echo "========= System Information ========="
talosctl get systemInformation
echo ""
echo ""

for NODE in "${NODES[@]}"; do
  NODE="${NODE//[[:space:]]/}"
  [[ -z "$NODE" ]] && continue

  echo "========= $NODE ========="
  echo "------ Hostname ------"
  talosctl get hostname --nodes "$NODE"
  echo "------ CPU ------"
  talosctl get processors --nodes "$NODE"
  echo "------ Memory ------'"
  talosctl get memorymodules --nodes "$NODE"
  echo "------ Disks ------"
  talosctl get disks --nodes "$NODE"
  echo ""
  echo ""
done
