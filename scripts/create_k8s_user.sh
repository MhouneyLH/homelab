#!/bin/bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: create_k8s_user.sh --user <name> [--groups <g1,g2>] [--out <dir>] [--context <kubecontext>] [--csr-name <name>]

Creates a client certificate, submits a CSR, approves it, and writes a kubeconfig.
Requires kubectl configured with admin access.
EOF
}

USER_NAME=""
GROUPS=""
OUT_DIR=""
KUBE_CONTEXT=""
CSR_NAME=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -u|--user)
      USER_NAME="$2"
      shift 2
      ;;
    -g|--groups)
      GROUPS="$2"
      shift 2
      ;;
    -o|--out)
      OUT_DIR="$2"
      shift 2
      ;;
    -c|--context)
      KUBE_CONTEXT="$2"
      shift 2
      ;;
    --csr-name)
      CSR_NAME="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$USER_NAME" ]]; then
  usage
  exit 1
fi

if [[ -z "$OUT_DIR" ]]; then
  OUT_DIR="./k8s-users/${USER_NAME}"
fi

if [[ -z "$CSR_NAME" ]]; then
  CSR_NAME="${USER_NAME}-$(date +%Y%m%d%H%M%S)"
fi

if [[ -z "$KUBE_CONTEXT" ]]; then
  KUBE_CONTEXT="$(kubectl config current-context)"
fi

umask 077
mkdir -p "$OUT_DIR"

KEY_FILE="${OUT_DIR}/${USER_NAME}.key"
CSR_FILE="${OUT_DIR}/${USER_NAME}.csr"
CERT_FILE="${OUT_DIR}/${USER_NAME}.crt"
KUBECONFIG_OUT="${OUT_DIR}/${USER_NAME}.kubeconfig"

SUBJECT="/CN=${USER_NAME}"
IFS=',' read -r -a GROUP_ARRAY <<< "$GROUPS"
for GROUP in "${GROUP_ARRAY[@]}"; do
  GROUP_TRIMMED="$(echo "$GROUP" | sed 's/^ *//; s/ *$//')"
  if [[ -n "$GROUP_TRIMMED" ]]; then
    SUBJECT="${SUBJECT}/O=${GROUP_TRIMMED}"
  fi
done


# Generate private key and CSR
openssl genrsa -out "$KEY_FILE" 2048
openssl req -new -key "$KEY_FILE" -out "$CSR_FILE" -subj "$SUBJECT"

CSR_B64="$(base64 < "$CSR_FILE" | tr -d '\n')"

# creating csr object and approving it
cat <<EOF | kubectl apply -f -
apiVersion: certificates.k8s.io/v1
kind: CertificateSigningRequest
metadata:
  name: ${CSR_NAME}
spec:
  request: ${CSR_B64}
  signerName: kubernetes.io/kube-apiserver-client
  usages:
  - client auth
EOF

kubectl certificate approve "$CSR_NAME"

for _ in {1..10}; do
  CERT_DATA="$(kubectl get csr "$CSR_NAME" -o jsonpath='{.status.certificate}')"
  if [[ -n "$CERT_DATA" ]]; then
    echo "$CERT_DATA" | base64 -d > "$CERT_FILE"
    break
  fi
  sleep 1
done

if [[ ! -s "$CERT_FILE" ]]; then
  echo "Certificate was not issued. Check CSR status: kubectl get csr ${CSR_NAME}" >&2
  exit 1
fi

CLUSTER_NAME="$(kubectl config view -o jsonpath="{.contexts[?(@.name==\"$KUBE_CONTEXT\")].context.cluster}")"
CLUSTER_SERVER="$(kubectl config view -o jsonpath="{.clusters[?(@.name==\"$CLUSTER_NAME\")].cluster.server}")"
CA_DATA="$(kubectl config view --raw -o jsonpath="{.clusters[?(@.name==\"$CLUSTER_NAME\")].cluster.certificate-authority-data}")"
CA_FILE="$(kubectl config view --raw -o jsonpath="{.clusters[?(@.name==\"$CLUSTER_NAME\")].cluster.certificate-authority}")"

if [[ -n "$CA_DATA" ]]; then
  CA_PATH="${OUT_DIR}/ca.crt"
  echo "$CA_DATA" | base64 -d > "$CA_PATH"
elif [[ -n "$CA_FILE" ]]; then
  CA_PATH="$CA_FILE"
else
  echo "Could not determine cluster CA data or file path." >&2
  exit 1
fi

kubectl config set-cluster "$CLUSTER_NAME" \
  --server="$CLUSTER_SERVER" \
  --certificate-authority="$CA_PATH" \
  --embed-certs=true \
  --kubeconfig "$KUBECONFIG_OUT"

kubectl config set-credentials "$USER_NAME" \
  --client-certificate="$CERT_FILE" \
  --client-key="$KEY_FILE" \
  --embed-certs=true \
  --kubeconfig "$KUBECONFIG_OUT"

kubectl config set-context "${USER_NAME}@${CLUSTER_NAME}" \
  --cluster="$CLUSTER_NAME" \
  --user="$USER_NAME" \
  --kubeconfig "$KUBECONFIG_OUT"

kubectl config use-context "${USER_NAME}@${CLUSTER_NAME}" --kubeconfig "$KUBECONFIG_OUT"

echo "Created kubeconfig: $KUBECONFIG_OUT"
echo "CSR name: $CSR_NAME"
