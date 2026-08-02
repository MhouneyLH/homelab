# TODO

## Argo CD Image Updater

Currently, deploying a new HomelabBrain.Api image tag means manually re-running a
`kubectl patch application homelab-brain ...` after every push (see
[`src/k8s/apps/services/homelab-brain/README.md`](./src/k8s/apps/services/homelab-brain/README.md#image-reference-kept-out-of-git)) -
nothing in git changes when a new image is pushed, so ArgoCD's `selfHeal` has nothing to react to.

[Argo CD Image Updater](https://argocd-image-updater.readthedocs.io/) would solve this properly:
watches a registry (Harbor is supported) for new tags matching a pattern and updates the
Application's image parameter automatically - no manual patch needed.

Separate concern from secrets/encryption - not something to force into the
[Sealed Secrets](./README.md#secrets-management) model (see homelab-brain's README for why the
image tag specifically can't be a `SealedSecret`).

Not set up yet - worth doing if the manual step gets old.
