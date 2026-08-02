# HomelabBrain (in-cluster deployment)

Deploys [`HomelabBrain.Api`](../../../../HomelabBrain/HomelabBrain.Api) from the private
`homelab-brain` project on the [internal Harbor registry](../../platform/harbor). Managed as a
[Kustomize](https://kustomize.io/) base (not a raw manifest `path:` - see why below), synced by
the ArgoCD `Application` at
[`../../../argocd-apps/homelab-brain.yml`](../../../argocd-apps/homelab-brain.yml).

## Image reference (kept out of git)

`deployment.yml` commits a placeholder: `image: homelab-brain/api:placeholder`. The real
registry host (your worker node's IP) and tag are set directly on the live ArgoCD `Application`
object instead - never committed, same reasoning as
[Harbor's secrets](../../platform/harbor/README.md#secrets-not-committed-to-git):

```bash
kubectl patch application homelab-brain -n argocd --type merge -p \
  '{"spec":{"source":{"kustomize":{"images":["homelab-brain/api=<worker-node-ip>:30002/homelab-brain/api:<tag>"]}}}}'
```

Re-run this (with a new `<tag>`) every time you push a new image - ArgoCD's `selfHeal` won't
pick up a new tag on its own, since nothing in git changed.

Why not just template the IP into a committed file with something like `.env`: raw Kubernetes
manifests have no templating step, and ArgoCD's `path:` source applies committed YAML literally -
there's nowhere for a local `.env` to be read from. Kustomize + a live-only Application override
is the GitOps-native equivalent - see the main
[HomelabBrain.Api Dockerfile / Harbor push docs](../../platform/harbor/README.md) for the
image-naming scheme this pairs with.

## Pull secret (also not committed)

The `homelab-brain` Harbor project is private, so the cluster needs credentials to pull from it -
created directly, referenced by name only via `imagePullSecrets` in `deployment.yml`. Use a
Harbor **Robot Account** (scoped, revocable, purpose-built for exactly this), not a personal
user account:

```bash
# Create the robot account (pull-only on this project). "secret" in the response is shown
# once - save it immediately, Harbor never shows it again.
curl -u admin:<harbor-admin-password> -X POST \
  http://<worker-node-ip>:30002/api/v2.0/projects/homelab-brain/robots \
  -H "Content-Type: application/json" \
  -d '{
    "name": "cluster-pull",
    "duration": -1,
    "permissions": [{"kind": "project", "namespace": "homelab-brain", "access": [{"resource": "repository", "action": "pull"}]}]
  }'

# Username comes back as robot$homelab-brain+cluster-pull
kubectl create secret docker-registry harbor-pull-secret \
  --docker-server=<worker-node-ip>:30002 \
  --docker-username='robot$homelab-brain+cluster-pull' \
  --docker-password=<secret-from-response> \
  -n services
```

## Node-level registry trust

Talos nodes also need to trust this registry (it's plain HTTP, no TLS) before `containerd` can
pull from it at all - a machine-config patch, not a Kubernetes object. See
[`src/talos/worker_nodes.tf`](../../../talos/worker_nodes.tf) and the main
[README](../../../../README.md) for that piece; without it, expect `ImagePullBackOff` regardless
of how correct the Secret/image reference above are.

## Config

`Mqtt:BrokerHost`/`DeviceConfigMqtt:BrokerHost` point at the standalone
[`mosquitto`](../../services/mosquitto) Service already running in this namespace
(`mosquitto.services.svc.cluster.local:1883`) - not secrets, so a plain `ConfigMap`
(`configmap.yml`), not a `Secret`.
