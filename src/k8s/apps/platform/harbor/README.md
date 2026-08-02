# Harbor

[Harbor](https://goharbor.io/) container registry, deployed via the
[official Helm chart](https://github.com/goharbor/harbor-helm) through
[`../../../argocd-apps/harbor.yml`](../../../argocd-apps/harbor.yml) (ArgoCD `Application`,
chart values from this directory's [`values.yml`](./values.yml)).

## Access

Internal-only for now (see main [README](../../../../../README.md)'s "Managing internal
communication" NodePort table) - no ingress, no TLS, no public DNS. Reachable from any device on
the LAN:

- UI/registry: `http://<worker-node-ip>:30002`
- Docker/OCI login: `docker login <worker-node-ip>:30002`
- Username `admin`, password: see `harbor-admin-password` Secret (below) - **not** the chart's
  documented default (`Harbor12345`), that was only used for the first ~15 minutes before this
  was locked down (see "What Was Broken" below).

### Pushing images from your local machine

Two one-time setup steps, then a normal `docker build`/`push`.

**1. Trust the registry.** It's plain HTTP (no TLS, since it's internal-only) - Docker refuses to
talk to a registry over HTTP unless told to. Linux native Docker:

```bash
sudo tee /etc/docker/daemon.json <<'EOF'
{
  "insecure-registries": ["<worker-node-ip>:30002"]
}
EOF
sudo systemctl restart docker
```

If `/etc/docker/daemon.json` already has content, merge the `insecure-registries` key in rather
than overwriting the file. Docker Desktop: Settings -> Docker Engine, edit the JSON there, Apply
& Restart.

**2. Create the project.** Pushing doesn't auto-create it:

```bash
curl -u admin:<harbor-admin-password> -X POST \
  http://<worker-node-ip>:30002/api/v2.0/projects \
  -H "Content-Type: application/json" \
  -d '{"project_name": "homelab-brain", "public": false}'
```

**Naming scheme**: `<registry>/<project>/<repository>:<tag>`. Project = access-control/quota
boundary, one per app (`homelab-brain`, not one Harbor project per image). Repository = image
name inside the project - keep it short since the project already scopes it (`api`, not
`homelab-brain-api`). Tag with the git short SHA for traceability rather than relying only on
`:latest` (mutable, not traceable to a commit) - push both, `:latest` as a floating convenience
pointer alongside the immutable SHA tag:

```bash
cd src/HomelabBrain
SHA=$(git rev-parse --short HEAD)
docker build -f HomelabBrain.Api/Dockerfile \
  -t <worker-node-ip>:30002/homelab-brain/api:$SHA \
  -t <worker-node-ip>:30002/homelab-brain/api:latest \
  .
docker login <worker-node-ip>:30002
docker push <worker-node-ip>:30002/homelab-brain/api:$SHA
docker push <worker-node-ip>:30002/homelab-brain/api:latest
```

Build context must be `src/HomelabBrain/` (not `HomelabBrain.Api/`) - the Dockerfile `COPY`s
sibling projects it references, which only works if they're inside the build context. See
[`HomelabBrain.Api/Dockerfile`](../../../../HomelabBrain/HomelabBrain.Api/Dockerfile)'s own
top comment for both invocation forms (from repo root vs. from `src/HomelabBrain/`).

## Secrets (not committed to git)

Two values the Helm chart needs are real secrets, so they're created directly in the cluster and
only *referenced by name* in `values.yml` (`existingSecretAdminPassword`,
`existingSecretSecretKey`) - never as plaintext in a file that goes to GitHub:

```
kubectl create secret generic harbor-secret-key -n platform \
  --from-literal=secretKey="<16-char random string>"
kubectl create secret generic harbor-admin-password -n platform \
  --from-literal=HARBOR_ADMIN_PASSWORD="<random password>"
```

`secretKey` **must be exactly 16 characters** (chart requirement - it's an AES key). It encrypts
sensitive data at rest (registry replication credentials, robot account secrets, OIDC/LDAP
secrets). Both values are only read by Harbor **once, on first database initialization** -
changing the Secret or `values.yml` afterward has no effect on an already-bootstrapped database:

- **Admin password**: change it live instead, via the UI (Administration -> Users -> root user ->
  Change Password) or `PUT /api/v2.0/users/{id}/password`. No redeploy needed - the Helm value
  only ever seeded the *initial* password.
- **secretKey**: no live rotation API. To change it on a database with real data: note/re-enter
  anything encrypted with it (replication endpoint credentials, robot account secrets, OIDC/LDAP
  secrets) *before* rotating, swap the Secret, restart `harbor-core`, then re-save those so they
  get re-encrypted under the new key. Disruptive to those integrations specifically, not a full
  wipe.

If these two Secrets don't exist yet (fresh cluster, restored from backup without them, etc.),
Harbor's `core`/`jobservice`/`registry` pods will `CrashLoopBackOff` on missing secret volume
mounts - create them first, before syncing the ArgoCD `Application`.

## What Was Broken (first setup)

Three separate issues, found and fixed in this order - useful if a fresh cluster hits the same
thing:

1. **`local-path` StorageClass not set as cluster default.** `values.yml` originally had no
   `persistence.persistentVolumeClaim.*.storageClass`, so `redis`/`trivy`/`database`/`registry`/
   `jobservice` PVCs all had an empty `storageClassName` and sat `Pending` forever
   (`0/2 nodes are available: pod has unbound immediate PersistentVolumeClaims`). Same pattern
   [`mosquitto`](../../services/mosquitto), [`gramps-backup`](../../services/gramps-backup), and
   [`prometheus`](../../monitoring/prometheus) already work around: set `storageClass: local-path`
   explicitly per component.

2. **StatefulSet `volumeClaimTemplates` are immutable.** Setting the storageClass in `values.yml`
   and syncing wasn't enough for `redis`/`trivy`/`database` (all StatefulSets, unlike `registry`/
   `jobservice` which are plain Deployments) - Kubernetes rejects any patch to
   `spec.volumeClaimTemplates` on an existing StatefulSet
   (`Forbidden: updates to statefulset spec for fields other than 'replicas', ...`). ArgoCD just
   retries and fails forever. Fix: delete the StatefulSet *and* its PVC (safe here - `Pending`
   with no bound volume means no data ever existed), let ArgoCD recreate both fresh from the
   corrected chart render.

3. **Default chart placeholders were live.** `externalURL`/`expose.ingress.hosts.core` were still
   `core.harbor.domain` (unreachable), and - the important one - `secretKey`/`harborAdminPassword`
   were still the chart's public documented defaults (`not-a-secure-key` /
   `Harbor12345`). Since a database had already bootstrapped with those defaults during step 2's
   fix cycle, and nothing real was stored yet, the cleanest fix was one more `harbor-database`
   StatefulSet+PVC wipe *after* wiring the real secrets in, so the fresh bootstrap picks them up
   correctly from the start (see "Secrets" above for how to change them without a wipe once real
   data exists).

**Ingress/TLS, tried then reverted.** First pass exposed Harbor publicly:
`expose.ingress.className: traefik` (cluster's ingress controller, not the chart's
nginx-oriented defaults) + `cert-manager.io/cluster-issuer: letsencrypt-prod-issuer` annotation +
`expose.tls.certSource: secret` with `expose.tls.secret.secretName` - same
[cert-manager](https://cert-manager.io/)+[Traefik](https://traefik.io/) HTTP-01 pattern
[`grampsweb`](https://gramps.lucas-festung.dynv6.net) uses in this cluster
(`kubectl get ingress grampsweb -n services -o yaml` to compare). Deliberately reverted to
internal-only `expose.type: nodePort` (see "Access" above) - not ready to expose this publicly
yet. Re-adding the ingress config above is enough to switch back, once that changes.
