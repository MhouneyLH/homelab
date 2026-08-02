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
   - [Gramps Backup and Restore](#gramps-backup-and-restore)
   - [RBAC and access management](#rbac-and-access-management)
   - [Secrets Management](#secrets-management)
5. [Helpful Commands](#helpful-commands)
6. [Development](#development)
   - [Working with talosctl](#working-with-talosctl)
7. [Learnings](#learnings)
   - [Ventoy _Fiebertraum_](#ventoy-fiebertraum)
8. [Additional References](#additional-references)

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

| Role          | Node IP           | Hostname          | Product Name               | CPU                                                   | Memory                   | Primary Disk                        |
| ------------- | ----------------- | ----------------- | -------------------------- | ----------------------------------------------------- | ------------------------ | ----------------------------------- |
| Control Plane | `pssst; see .env` | `hl-controlplane` | Dell OptiPlex 3050         | Intel Core i3-7100T (2 cores / 4 threads) at 3.4 GHz  | 4 GiB (1 x 4 GiB DIMM)   | `sda` SATA 128 GB (SK hynix SC311)  |
| Worker        | `pssst; see .env` | `hl-worker-01`    | HP EliteDesk 800 G2 DM 35W | Intel Core i7-6700T (4 cores / 8 threads) at 2.80 GHz | 16 GiB (2 x 8 GiB DIMMs) | `nvme0n1` NVMe 512 GB (NX-512 2280) |

> [!NOTE]
> **When trying to get overview again:** Use the [get_node_information.sh](./scripts/get_node_information.sh) script to get an instant overview of the hardware information of the nodes which ip-addresses can be specified in the [.env](./.env) file. If you want to run commands manually, also take a look at the [Working with talosctl](#working-with-talosctl) section.

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
  - OpenTelemetry Collector for collecting and forwarding metrics and logs to the right places

## Prerequisites

- Terraform installed e.g. on the local machine / jump host
- [kubectl](https://kubernetes.io/docs/setup/) installed on the local machine / jump host for accessing the cluster
- Prepared Talos Linux ISO image (see [here](https://docs.siderolabs.com/talos/v1.9/platform-specific-installations/bare-metal-platforms/iso) for more information)
- Ventoy installed on a USB stick (see [here](https://www.ventoy.net/en/download.html) for more information)
- (_optional, but recommended_) [talosctl](https://docs.siderolabs.com/talos/v1.8/getting-started/talosctl) installed on the local machine / jump host for easier management of the cluster
- (_optional, but recommended_) tools for managing the cluster like e.g. [k9s](https://k9scli.io/) or [Headlamp](https://headlamp.dev/docs/latest/installation/) installed on the local machine / jump host for easier management of the cluster

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
  - [rbac](./src/k8s/rbac): cluster-wide RBAC (ClusterRoles and ClusterRoleBindings). App-specific RBAC is co-located with the app or infra component.

### Bootstrapping

Before running Terraform, keep node IPs in local ignored tfvars files:

```bash
cp src/talos/secrets.auto.tfvars.example src/talos/secrets.auto.tfvars
cp src/bootstrap/secrets.auto.tfvars.example src/bootstrap/secrets.auto.tfvars
```

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

Once ArgoCD is running, deploy all applications by applying the root app (app-of-apps pattern):

```bash
kubectl apply -f src/k8s/root-app.yaml
```

ArgoCD will pick this up and automatically deploy everything in `src/k8s/argocd-apps/`.

### Accessing the cluster

For being able to access the cluster, you need to have the kubeconfig file. You can get it from the terraform output like this:

```bash
cd src/talos
terraform output -raw kubeconfig > ~/.kube/config
chmod 600 ~/.kube/config
```

You can also use the [talosctl](https://docs.siderolabs.com/talos/v1.6/learn-more/talosctl) cli tool like this:

```bash
talosctl kubeconfig ~/.kube/config -n the-node-ip-address
chmod 600 ~/.kube/config
```

### Being accessible from the outside world

Quick checks for public IP vs DNS:

```bash
curl -4 https://ifconfig.me
dig +short gramps.lucas-festung.dynv6.net A
```

> [!NOTE]
> In the past we realized some problems that the dyndns actually updates the IP address correctly. This is why I often checked if the public ip address is actually the same as the one in the DNS record.

### Managing internal communication

Some applications aren't meant to be reachable from the outside world (yet, or ever) - those get
exposed as a `NodePort` `Service` instead of going through the [ingress
setup](#being-accessible-from-the-outside-world). Reachable from any device on the LAN at
`<node-ip>:<node-port>` - node IPs aren't published in this repo (see [Hardware](#hardware)), so
substitute your own from `.env` / `get_node_information.sh`.

| Application | Namespace    | Node Port(s)  | Notes                                                                   |
| ----------- | ------------ | ------------- | ------------------------------------------------------------------------ |
| ArgoCD      | `argocd`     | 30080 / 30443 | GitOps UI                                                                |
| n8n         | `automation` | 30712         | Automation tool ([more](#n8n))                                          |
| Prometheus  | `monitoring` | 30090         | Metrics                                                                  |
| Grafana     | `monitoring` | 30070         | Dashboards ([more](#grafana))                                           |
| Mosquitto   | `services`   | 30001 / 30782 | MQTT broker + websocket, for LAN devices (e.g. the gardening firmware)  |
| Traefik     | `traefik`    | 30081 / 30444 | Ingress controller's own HTTP/HTTPS entrypoints, plus 31883 for MQTT    |
| Harbor      | `platform`   | 30002         | Container registry - internal-only for now, no ingress/TLS/public DNS  |
| HomelabBrain| `services`   | 30003         | REST API ([more](./src/k8s/apps/services/homelab-brain))               |

Check current state any time: `kubectl get svc -A --field-selector spec.type=NodePort` (see
[Helpful Commands](#helpful-commands)).

### Managing Persistent Storage

Currently some Helm Charts like e.g. the GrampsWeb Chart are using the [local-path-provisioner](./src/k8s/argocd-apps/local-path-provisioner.yml) for provisioning Persistent Volumes. This is a great solution for testing and learning purposes, but in the future I want to have a more robust solution for this.

### Gramps Backup and Restore

#### How is the backup currently done?

For GrampsWeb I currently use a local in-cluster backup mechanism (no cloud dependency yet).

- backup app manifests: [gramps-backup Application](./src/k8s/argocd-apps/gramps-backup.yml)
- backup workload manifests: [CronJob + PVC](./src/k8s/apps/services/gramps-backup)

What is backed up:

- users database and auth data (`/app/users`)
- search/index/cache/secret data (`/app/indexdir`, `/app/thumbnail_cache`, `/app/secret`)
- media files (`/app/media`)
- Gramps sqlite databases and config (`/app/db`, `/app/persist`, `/app/config`)
- Gramps family tree database (`/root/.gramps/grampsdb`) — **this is the actual genealogy data**

How backups work (storage):

- nightly CronJob (`gramps-backup`) writes `tar.gz` archives at 2am into `/backup/archives` on the `gramps-backup` PVC
- retention deletes archives older than 30 days
- the backup PVC is marked with `Delete=false,Prune=false` so it is not accidentally removed by ArgoCD pruning

Manual backup run:

```bash
kubectl -n services create job --from=cronjob/gramps-backup gramps-backup-manual-$(date +%s)
kubectl -n services get jobs,pods | grep gramps-backup
```

Inspect existing archives:

```bash
kubectl -n services apply -f src/k8s/apps/services/gramps-backup/backup-helper-pod.yml
kubectl -n services exec deploy/gramps-backup-helper -- ls -lh /backup/archives
```

#### How to restore when things break?!

> [!WARNING]
> **Do NOT restore `users.sqlite` via the helper pod.** Grampsweb runs alembic migrations on every startup and reinitializes `users.sqlite`, wiping any file you restored while the pod was down. Users must be injected directly into the live running pod after startup. See the users restore section below.

> [!WARNING]
> **Do NOT open `/firstrun` in the browser after the pod starts.** This triggers grampsweb to overwrite `users.sqlite` with a fresh user. Always navigate directly to `/login` instead — this skips the firstrun flow and preserves any users already in the database.

**Restore media, family tree DB, and other data:**

```bash
# 1) Stop writes
kubectl -n services scale deployment/grampsweb --replicas=0

# 2) Start the helper pod (mounts backup PVC at /backup and grampsweb PVC at /source)
kubectl -n services apply -f src/k8s/apps/services/gramps-backup/backup-helper.yml

# 3) List available archives and pick one
kubectl -n services exec deploy/gramps-backup-helper -- ls -lh /backup/archives

# 4) Restore selected archive into the grampsweb PVC (excludes users.sqlite — restored separately)
kubectl -n services exec deploy/gramps-backup-helper -- tar -xzf /backup/archives/<your-archive>.tar.gz \
  --exclude=app/users/users.sqlite -C /source

# 5) Verify files landed correctly
kubectl -n services exec deploy/gramps-backup-helper -- find /source -type f

# 6) Clean up helper and start Gramps again
kubectl -n services delete deployment gramps-backup-helper --ignore-not-found
kubectl -n services scale deployment/grampsweb --replicas=1
kubectl -n services rollout status deployment/grampsweb
```

**Restore users (must be done into the live running pod):**

```bash
# 1) Extract user rows from a backup archive
kubectl -n services apply -f src/k8s/apps/services/gramps-backup/backup-helper.yml
kubectl -n services exec deploy/gramps-backup-helper -- \
  tar -xzf /backup/archives/<your-archive>.tar.gz -C /tmp app/users/users.sqlite
kubectl -n services exec deploy/gramps-backup-helper -- \
  sqlite3 /tmp/app/users/users.sqlite 'SELECT id,name,email,fullname,pwhash,role,tree FROM users;'

# 2) Make sure grampsweb is running (so it has already completed its startup migrations)
kubectl -n services rollout status deployment/grampsweb

# 3) Inject the users directly into the live pod
kubectl -n services exec deploy/grampsweb -- python3 -c "
import sqlite3
conn = sqlite3.connect('/app/users/users.sqlite')
cur = conn.cursor()
cur.execute(\"INSERT OR IGNORE INTO trees (id) VALUES ('<tree-uuid>')\")
cur.executemany(
  'INSERT OR REPLACE INTO users (id,name,email,fullname,pwhash,role,tree) VALUES (?,?,?,?,?,?,?)',
  [
    ('<id>', '<name>', '<email>', '<fullname>', '<pwhash>', <role>, '<tree>'),
    # ... one tuple per user from step 1
  ]
)
conn.commit()
cur.execute('SELECT name, role FROM users')
print(cur.fetchall())
conn.close()
"
```

> [!NOTE]
> **To myself:** Test the goddamn restore regularly! Backup without restore test is not enough. :(

### RBAC and access management

Cluster-wide access rules live in [src/k8s/rbac](src/k8s/rbac). This is the place for ClusterRoles and ClusterRoleBindings that apply across namespaces.

App-specific RBAC should be co-located with the app or infrastructure component under [src/k8s/apps](src/k8s/apps) or [src/k8s/infrastructure](src/k8s/infrastructure).

#### How to grant access (example: read-only)

The read-only example uses the cluster-wide role and user-specific binding defined in [src/k8s/rbac/resource-readers.yml](src/k8s/rbac/resource-readers.yml).

### Secrets Management

Real secret *values* never get committed to this repo - that's true everywhere in it, not just
for one app. Two different mechanisms, depending on the situation:

- **Plain `kubectl create secret` against the live cluster, name-only reference in git.** The
  original approach, still used for anything created once and rarely touched (e.g.
  [Harbor's admin password/secretKey](./src/k8s/apps/platform/harbor/README.md#secrets-not-committed-to-git)).
  Simple, but "recreate the same secret" isn't reviewable, diffable, or reproducible from git
  alone - someone has to remember it happened and how.
- **[Sealed Secrets](https://github.com/bitnami/sealed-secrets)**, for anything else -
  particularly per-app secrets (like an image-pull credential) that should live and travel with
  that app's manifests. An in-cluster controller
  ([`src/k8s/argocd-apps/sealed-secrets.yml`](./src/k8s/argocd-apps/sealed-secrets.yml),
  [values](./src/k8s/apps/infrastructure/sealed-secrets/values.yml)) holds an asymmetric keypair;
  you encrypt a secret locally with its public key via the `kubeseal` CLI, and the **encrypted**
  `SealedSecret` object is what gets committed - safe to put in a public repo, since only the
  controller's private key (never leaves the cluster) can decrypt it. ArgoCD applies it like any
  other manifest; the controller decrypts it into a normal `Secret` in-cluster automatically.

```bash
# One-time: install kubeseal (match the version to the controller's appVersion, see the
# chart version pinned in sealed-secrets.yml above)
curl -sSL -o kubeseal.tar.gz \
  https://github.com/bitnami/sealed-secrets/releases/download/v0.38.4/kubeseal-0.38.4-linux-amd64.tar.gz
tar -xzf kubeseal.tar.gz kubeseal && sudo install kubeseal /usr/local/bin/kubeseal

# Seal a secret (dry-run creates the plain Secret locally, never sent to the cluster as
# plaintext; kubeseal encrypts it, only the *encrypted* SealedSecret gets applied/committed)
kubectl create secret generic <name> -n <namespace> \
  --from-literal=<key>=<value> --dry-run=client -o yaml \
  | kubeseal --controller-name=sealed-secrets --controller-namespace=sealed-secrets \
      --format yaml > <name>-sealed.yml

kubectl apply -f <name>-sealed.yml   # or just commit it - ArgoCD will
```

**Back up the controller's private key** (`kubectl get secret -n sealed-secrets -l
sealedsecrets.bitnami.com/sealed-secrets-key -o yaml > sealing-key-backup.yml`, store it
somewhere safe, *outside* git) - lose it with no backup, and every `SealedSecret` ever committed
becomes permanently undecryptable, cluster rebuild or not.

## Helpful Commands

Quick reference for things reached for often when operating the cluster.

```bash
# List every NodePort service across all namespaces (see the table above)
kubectl get svc -A --field-selector spec.type=NodePort

# Force ArgoCD to re-sync an Application right now instead of waiting for its poll interval
kubectl annotate application <app-name> -n argocd argocd.argoproj.io/refresh=hard --overwrite

# Check an ArgoCD Application's sync/health status and the reason it's out of sync, if any
kubectl get application <app-name> -n argocd -o jsonpath='{.status.sync.status} {.status.health.status}{"\n"}{.status.operationState.message}{"\n"}'

# Pods, logs, and events for a namespace - the usual first three checks when something's broken
kubectl get pods -n <namespace>
kubectl logs -n <namespace> <pod-name>
kubectl describe pod -n <namespace> <pod-name>
```

Set the `subjects.name` in the RoleBinding to the username (CN) you issue in the client cert. The current file uses a placeholder (`homelab-read-all-user`) that should be replaced with a real username.

To give a person access, issue a client certificate where the CN matches the username referenced in the RoleBinding. Groups (O=) are optional. Use the helper script to create a user cert and kubeconfig (groups optional): [scripts/create_k8s_user.sh](scripts/create_k8s_user.sh).

```bash
bash scripts/create_k8s_user.sh --user some-username
```

To check RBAC before giving out the cert, verify with `kubectl auth can-i`:

```bash
kubectl auth can-i list pods --as=some-username
```

#### How to revoke access

With client cert auth there is no native per-cert revocation list. The practical way to revoke access is to remove the user's RoleBinding (or delete the user-specific binding). That keeps authentication working but authorization fails.

If you need to fully invalidate a stolen cert, rotate the cluster CA (or switch to an auth provider that supports real revocation) and re-issue user certs. Deleting the CSR object does not revoke an already issued certificate.

#### Why user-specific bindings

User bindings make offboarding simple: delete a single RoleBinding and access is gone. The tradeoff is more bindings to manage. Groups are still possible if the set of users changes frequently.

## Development

### Working with talosctl

```bash
terraform output -raw kubeconfig > ~/.kube/config # export kubeconfig from terraform output
chmod 600 ~/.kube/config # set permissions for kubeconfig
kubectl get nodes # verify cluster access

mkdir -p ~/.talos
talosctl dashboard -n the-ip-of-the-node # when endpoint for talosctl is defined in config

talosctl config info

# getting overview, which nodes exist in the cluster
talosctl get members

# getting hardware info of a node
talosctl get systemInformation -n the-ip-of-the-node

# CPU info
talosctl get processors --nodes the-ip-of-the-nodes-comma-separated
# RAM / memory modules
talosctl get memorymodules --nodes the-ip-of-the-nodes-comma-separated
# list disks of the node - nice for finding out which disk is the one you want to install Talos on
talosctl get disks -n the-ip-of-the-node --insecure
# getting MAC address of a node
talosctl get links -n the-ip-of-the-node

# editing the machine configuration manually (sadly not possible to adjust the hostname on this way; for this you have to connect directly with the node and edit network config there)
talosctl -n the-ip-of-the-node edit mc --mode=staged
```

## Learnings

Next to learning more and more about k8s itself, there were some things I had to learn the hard way...

### Node IP Change Broke Internal Cluster Networking

When the IPs of the nodes changed, the cluster broke internally — pods could no longer reach the Kubernetes API server via its internal ClusterIP (`10.96.0.1:443`), which meant things like ArgoCD Redis couldn't start at all.

**What happened step by step:**

- Node IPs changed → Talos machine config was re-applied with new IPs → cluster certs got regenerated
- `kube-proxy` (the component that routes traffic from ClusterIPs to real pod/node IPs) was running in `nftables` mode
- There is a bug in kube-proxy's nftables mode: for the special `kubernetes` service (which points to the API server node IP, not a pod IP), the DNAT rule inside the nftables chain was never written — the chain was completely empty
- Result: any pod trying to reach `10.96.0.1:443` (= the Kubernetes API) got `connection refused`

**How it was fixed:**

- Confirmed the issue by running a debug pod and checking nftables rules directly on the node — the endpoint chain for the kubernetes service was empty
- Switched kube-proxy from `nftables` mode to `iptables` mode by patching the DaemonSet: changed `--proxy-mode=nftables` → `--proxy-mode=iptables`
- After the rollout, the iptables DNAT rules were correctly created and pods could reach the API server again

**Extra mess along the way:**

- The old ArgoCD namespace was stuck in `Terminating` because an ArgoCD `Application` resource had a finalizer (`resources-finalizer.argocd.argoproj.io`) — and since ArgoCD itself was already gone, no controller could process it. Fixed by patching the finalizer list to empty: `kubectl patch application.argoproj.io root-app -n argocd -p '{"metadata":{"finalizers":[]}}' --type=merge`
- Stale Helm release secrets (`sh.helm.release.v1.argocd.v1`) blocked Terraform from reinstalling ArgoCD. Fixed by deleting the secret manually before re-running `terraform apply`
- The `workers` and `argocd_ui_host` Terraform variables were marked `sensitive = true`, but Terraform does not allow sensitive values as `for_each` keys or in outputs — removed `sensitive = true` from those variables

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
