# Grafana

Deployed via the [grafana Helm chart](https://github.com/grafana/helm-charts), through
[`../../../argocd-apps/grafana.yml`](../../../argocd-apps/grafana.yml).

## Admin credentials

`admin.existingSecret: grafana-admin` in [`values.yml`](./values.yml) references a
[Sealed Secret](../../../../../README.md#secrets-management), committed (encrypted) at
[`sealed-secrets/grafana-admin-sealed.yml`](./sealed-secrets/grafana-admin-sealed.yml), applied
via the third `sources[]` entry in `grafana.yml`. Previously a plain `secret.yml` template
(`${GRAFANA_ADMIN_USER}`/`${GRAFANA_ADMIN_PASSWORD}` placeholders, applied by hand with
`envsubst` - never actually wired into ArgoCD) - replaced, since the sealed version syncs
automatically like everything else instead of relying on someone remembering to run a command.

To rotate the admin user/password, or seed a fresh cluster:

```bash
kubectl create secret generic grafana-admin -n monitoring \
  --from-literal=admin-user=<username> \
  --from-literal=admin-password=<password> \
  --dry-run=client -o yaml \
  | kubeseal --controller-name=sealed-secrets --controller-namespace=sealed-secrets \
      --format yaml > sealed-secrets/grafana-admin-sealed.yml
```

Same caveat as [Harbor's secrets](../../platform/harbor/README.md#secrets): only decrypts
against the same [Sealed Secrets](../../infrastructure/sealed-secrets) controller keypair it was
encrypted with - back that key up (see main README).
