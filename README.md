# Homelab Setup

My homelab setup all in one repository. :)

This project helps me to continuously learn k8s and related technologies, and to have a playground for testing new tools and concepts. It also serves as a documentation of my homelab setup, so I can easily remember how everything is set up and how to do things.

## Table of Contents

1. [Actual Running Usable Applications](#actual-running-usable-applications)
   - [n8n](#n8n)
   - [Gramps](#gramps)
   - [Backstage](#backstage)
   - [Grafana](#grafana)
2. [Tech Stack](#tech-stack)
   - [Hardware](#hardware)
   - [Software](#software)
3. [Prerequisites](#prerequisites)
4. [Structure](#structure)
   - [Directory Hierarchy](#directory-hierarchy)
   - [Bootstrapping](#bootstrapping)
   - [Accessing the cluster](#accessing-the-cluster)
   - [Being accessible from the outside world](#being-accessible-from-the-outside-world)
   - [Managing internal communication](#managing-internal-communication)
   - [Managing Persistent Storage](#managing-persistent-storage)
   - [RBAC and access management](#rbac-and-access-management)
5. [Development](#development)
   - [Working with talosctl](#working-with-talosctl)
6. [Learnings](#learnings)
   - [Ventoy _Fiebertraum_](#ventoy-fiebertraum)
7. [Additional References](#additional-references)

## Actual Running Usable Applications

### n8n

This is my automation tool of choice for all the different things I want to automate in my homelab.

Examples are:

- daily summary of my life (steps walked, hours standing at desk, etc.)
- notifaction alerts when something goes wrong in the homelab or on other infrastructure
- chatting via telegram with my homelab to gettig stuff fixed
- daily improvements for telemetry of the homelab itself and its applications that are running in it

### Gramps

As I am interested in the history of my family, I started collecting information about my ancestors and relatives. After some time it was hard to keep up which is why I searched for already existing software to manage this information. Next to paid tools like [Ancestry](https://www.ancestry.de/), I found the free and open source tool [Gramps Web](https://www.grampsweb.org/). Especially when having to goal to let this grow more and more it totally makes sense to host this on my own infrastructure without any limitations in terms of number of people or amount of data.

The application is provided using a helm chart. (see [here](./src/k8s/argocd-apps/gramps.yml) for more information)

### Backstage

Backstage is a developer portal that helps to manage and discover all the different tools and applications in the homelab. It provides a single entry point for all the different applications and tools, and it also provides a way to manage access to these applications.

This is mainly here for the purpose of learning how to use & integrate it with other applications.

### Grafana

Grafana is my observability UI of choice. I use it to visualize all the different metrics and logs that I collect from my homelab. The broad range of available plugings & dashboards make it a great choice to get a nice overview of the different applications and tools in the homelab without much work.

## Tech Stack

### Hardware

Currently, my homelab consists of 2 nodes with the following specifications:

- Node 1 (Control Plane; `hl-control-plane`): TBD
- Node 2 (Worker; `hl-worker-01`): TBD

### Software

The Software stack consists of the following components:

- distro: [Talos Linux](https://docs.siderolabs.com/talos/v1.9/platform-specific-installations/bare-metal-platforms/iso) installed on the nodes
- configuration of the distros: Terraform using the [Talos Provider](https://registry.terraform.io/providers/siderolabs/talos/latest/docs)
- currently in total 1 Kubernetes Cluster running:
  - version: 1.35
  - no custom CNI (YET)
- applications are deployed using ArgoCD in a GitOps manner (utilizing the app-of-apps pattern)
- monitoring & observability:
  - Prometheus for metrics collection
  - Grafana for visualization of metrics and logs
  - OpenTelemtry Collector for collecting and forwarding metrics and logs to the right places

## Prerequisites

- Terraform installed e.g. on the local machine / jump host
- Prepared Talos Linux ISO image (see [here](https://docs.siderolabs.com/talos/v1.9/platform-specific-installations/bare-metal-platforms/iso) for more information)
- Ventoy installed on a USB stick (see [here](https://www.ventoy.net/en/download.html) for more information)

## Structure

### Directory Hierarchy

In the `src` directory, there are the distinction of different types of configurations:

- [talos](./src/talos): contains the configuration for the Talos Linux distros running on the nodes
- [bootstrap](./src/bootstrap): contains the terraform configuration for bootstrapping the cluster
- [k8s](./src/k8s): contains the kubernetes configuration for the cluster and the applications running on it
  - [root-app.yaml](./src/k8s/root-app.yaml): this is the root application for the app-of-apps pattern, which deploys all the other applications in the cluster via ArgoCD
  - [apps](./src/k8s/apps): these are the actual manifests (either written by hand or via helm charts) for the applications running in the cluster
  - [argocd-apps](./src/k8s/argocd-apps): these are the ArgoCD application manifests for actually deploying the applications in the cluster
  - [infrastructure](./src/k8s/infrastructure): these are the manifests for the infrastructure components of the cluster, e.g. CNI, namesspaces, etc. (PROBABLY THIS SHOULD BE HANDLED DIFFERENTLY; I CURRENTLY USE THIS FOR EVERYTHING I DONT KONW WHERE TO PUT)

### Bootstrapping

The bootstrapping of the cluster is handled via Terraform using the Talos Provider. Use the following commmands to bootstrap the cluster:

```bash
cd src/talos
terraform init
terraform apply
```

To bootstrap the other stuff on top (currently it is just ArgoCD, but in the future there will be more stuff like CNI, etc.) you can use the following commands:

```bash
cd src/bootstrap
terraform init
terraform apply
```

### Accessing the cluster

For being able to access the cluster, you need to have the kubeconfig file. You can get it from the terraform output like this:

```bash
cd src/talos
terraform output -raw kubeconfig > ~/.kube/config
chmod 600 ~/.kube/config
```

When not having access to the terraform state file, you can also use the [talosctl](https://docs.siderolabs.com/talos/v1.6/learn-more/talosctl) cli tool like this:

```bash
talosctl kubeconfig ~/.kube/config -n the-node-ip-address --insecure
chmod 600 ~/.kube/config
```

or when secure is possible:

```bash
talosctl kubeconfig ~/.kube/config -n the-node-ip-address
chmod 600 ~/.kube/config
```

### Being accessible from the outside world

TBD

### Managing internal communication

TBD

### Managing Persistent Storage

Currently some Helm Charts like e.g. the GrampsWeb Chart are using the [local-path-provisioner](./src/k8s/argocd-apps/local-path-provisioner.yml) for provisioning Persistent Volumes. This is a great solution for testing and learning purposes, but in the future I want to have a more robust solution for this.

### RBAC and access management

TBD

## Development

### Working with talosctl

```bash
talosctl get disks -n the-ip-of-the-node --insecure # list disks of the node - nice for finding out which disk is the one you want to install Talos on

terraform output -raw kubeconfig > ~/.kube/config # export kubeconfig from terraform output
chmod 600 ~/.kube/config # set permissions for kubeconfig
kubectl get nodes # verify cluster access

mkdir -p ~/.talos
talosctl dashboard -n the-ip-of-the-node # when endpoint for talosctl is defined in config

talosctl config info

# getting hardware info of a node
talosctl get systemInformation -n the-ip-of-the-node

# getting MAC address of a node
talosctl get links -n the-ip-of-the-node

# editing the machine configuration manually (sadly not possible to adjust the hostname on this way; for this you have to connect directly with the node and edit network config there)
talosctl -n the-ip-of-the-node edit mc --mode=staged
talosctl get members
```

## Learnings

Next to learning more and more about k8s itself, there were some things I had to learn the hard way...

### Ventoy _Fiebertraum_

Right after the beginning of the initial setup, I found out about [Ventoy](https://www.ventoy.net/en/index.html) which is a tool to have multiple ISO images on a USB stick and to be able to boot from them. This was a game changer for me, as I could easily switch between different ISO images. As I bought older hardware for my homelab, one device had problems with figuring out the correct boot order. (probably because it was irritated by the partition magic that is happening in the background of Ventoy) As I was to stubborn and wanted to try out my new knowledge, I spent at least 1 hour searching the error.

The soluation was just to use a good'ol bootable USB stick with the Talos Linux ISO image on it. After that, I could easily boot from the USB stick and install Talos Linux on the node.

## Additional References

- https://docs.siderolabs.com/talos/v1.9/platform-specific-installations/bare-metal-platforms/iso
- https://www.ventoy.net/en/download.html
- https://registry.terraform.io/providers/siderolabs/talos/latest/docs
- https://docs.siderolabs.com/talos/v1.12/getting-started/talosctl#alternative-install
- https://community-charts.github.io/
- https://github.com/EdJoPaTo/mqttui
