#!/usr/bin/env bash
# Applies the n8n owner secret to the cluster from a local .env file.
# Usage: ./scripts/apply-n8n-secrets.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/../src/k8s/apps/automation/n8n/.env"
TEMPLATE="$SCRIPT_DIR/../src/k8s/apps/automation/n8n/secret.yml"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Error: $ENV_FILE not found."
  echo "Copy the example and fill in the values:"
  echo "  cp src/k8s/apps/automation/n8n/.env.example src/k8s/apps/automation/n8n/.env"
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

envsubst < "$TEMPLATE" | kubectl apply -f -
echo "n8n-owner secret applied to namespace 'automation'."
