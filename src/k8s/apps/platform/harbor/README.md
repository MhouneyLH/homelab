# Harbor

[Harbor](https://goharbor.io/) container registry, deployed via the
[official Helm chart](https://github.com/goharbor/harbor-helm) through
[`../../../argocd-apps/harbor.yml`](../../../argocd-apps/harbor.yml) (ArgoCD `Application`,
chart values from this directory's [`values.yml`](./values.yml)).

## Access

- UI/registry: `https://harbor.lucas-festung.dynv6.net`
- Docker/OCI login: `docker login harbor.lucas-festung.dynv6.net`
- Username `admin`, password: see `harbor-admin-password` Secret (below) - **not** the chart's
  documented default (`Harbor12345`), that was only used for the first ~15 minutes before this
  was locked down (see "What Was Broken" below).

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

Ingress/TLS: `expose.ingress.className: traefik` (cluster's ingress controller, not the chart's
nginx-oriented defaults) + `cert-manager.io/cluster-issuer: letsencrypt-prod-issuer` annotation +
`expose.tls.certSource: secret` with `expose.tls.secret.secretName` - same
[cert-manager](https://cert-manager.io/)+[Traefik](https://traefik.io/) HTTP-01 pattern
[`grampsweb`](https://gramps.lucas-festung.dynv6.net) already uses in this cluster
(`kubectl get ingress grampsweb -n services -o yaml` to compare).
