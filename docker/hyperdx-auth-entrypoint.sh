#!/bin/sh
set -eu

username="${HYPERDX_USERNAME:-HyperDX}"
password="${HYPERDX_PASSWORD:-HyperDX}"
password_hash="$(caddy hash-password --plaintext "$password")"

cat > /tmp/Caddyfile <<EOF
:8080 {
  basic_auth {
    $username $password_hash
  }
  reverse_proxy clickstack:8080
}
EOF

exec caddy run --config /tmp/Caddyfile --adapter caddyfile
