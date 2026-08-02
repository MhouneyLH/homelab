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
- Admin login (`admin`, password: see `harbor-admin-password` Secret below - **not** the chart's
  documented default `Harbor12345`, that only lasted the first ~15 minutes before this was locked
  down, see "What Was Broken" below) is for cluster administration only: creating projects,
  creating user accounts, rotating secrets. Not for day-to-day pushing - see "Pushing images"
  below for that.

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

**3. Get a personal account.** Push with your own Harbor user, not the `admin` account -
`admin` is for cluster administration (creating projects, rotating secrets), not day-to-day
pushing. There's no self-service signup by design - ask whoever administers Harbor (currently:
ask me) to create an account and add it as a project member. Done as `admin` via:

```bash
# Create the user
curl -u admin:<harbor-admin-password> -X POST \
  http://<worker-node-ip>:30002/api/v2.0/users \
  -H "Content-Type: application/json" \
  -d '{"username": "<their-username>", "email": "<their-email>", "password": "<their-password>", "realname": "<their-name>"}'

# Grant push access on the homelab-brain project (role_id 2 = Developer - push/pull,
# no delete; use 3 for Maintainer if they also need to delete images)
curl -u admin:<harbor-admin-password> -X POST \
  http://<worker-node-ip>:30002/api/v2.0/projects/homelab-brain/members \
  -H "Content-Type: application/json" \
  -d '{"role_id": 2, "member_user": {"username": "<their-username>"}}'
```

This is for a human pushing from their own machine. For something automated pulling images
(a Deployment's `imagePullSecrets`, CI, etc.), use a **Robot Account** instead, not a personal
user - scoped to exactly the permission needed (usually pull-only) and independently revocable.
See [`homelab-brain`'s README](../../services/homelab-brain/README.md#pull-secret-also-not-committed)
for the exact commands.

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
docker login <worker-node-ip>:30002  # your personal account, not admin
docker push <worker-node-ip>:30002/homelab-brain/api:$SHA
docker push <worker-node-ip>:30002/homelab-brain/api:latest
```

Build context must be `src/HomelabBrain/` (not `HomelabBrain.Api/`) - the Dockerfile `COPY`s
sibling projects it references, which only works if they're inside the build context. See
[`HomelabBrain.Api/Dockerfile`](../../../../HomelabBrain/HomelabBrain.Api/Dockerfile)'s own
top comment for both invocation forms (from repo root vs. from `src/HomelabBrain/`).

## Secrets

Two values the Helm chart needs are real secrets, referenced by name only in `values.yml`
(`existingSecretAdminPassword`, `existingSecretSecretKey`) - the actual values live as
[Sealed Secrets](../../../../../README.md#secrets-management), committed (encrypted) at
[`sealed-secrets/`](./sealed-secrets), applied via the third `sources[]` entry in
[`harbor.yml`](../../../argocd-apps/harbor.yml) (the ArgoCD `Application`, not the
[Sealed Secrets controller](../../infrastructure/sealed-secrets)). To rotate either, or seed a
fresh cluster from scratch:

```bash
kubectl create secret generic harbor-secret-key -n platform \
  --from-literal=secretKey="<16-char random string>" --dry-run=client -o yaml \
  | kubeseal --controller-name=sealed-secrets --controller-namespace=sealed-secrets \
      --format yaml > sealed-secrets/harbor-secret-key-sealed.yml

kubectl create secret generic harbor-admin-password -n platform \
  --from-literal=HARBOR_ADMIN_PASSWORD="<random password>" --dry-run=client -o yaml \
  | kubeseal --controller-name=sealed-secrets --controller-namespace=sealed-secrets \
      --format yaml > sealed-secrets/harbor-admin-password-sealed.yml
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

Both `SealedSecret`s sync automatically along with everything else in `harbor.yml` - no separate
manual step, unlike before. One real caveat: they only decrypt against the *same*
[Sealed Secrets](../../infrastructure/sealed-secrets) controller keypair they were encrypted
with. A genuinely fresh cluster (new controller, new keypair) can't decrypt these committed files
at all until the old keypair is restored too - see the key-backup note in the main README's
Secrets Management section. Without a matching key, Harbor's `core`/`jobservice`/`registry` pods
will `CrashLoopBackOff` on missing secret volume mounts, same as before this existed.

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
