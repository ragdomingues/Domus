#!/usr/bin/env bash
# Rode no servidor Hetzner (como root), na pasta do repo:
#   cd /opt/domus && bash scripts/deploy-hetzner.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ ! -f .env ]]; then
  JWT="$(openssl rand -base64 48 | tr -d '\n=/+' | head -c 48)"
  PG="$(openssl rand -base64 24 | tr -d '\n=/+' | head -c 24)"
  MQTT_API="$(openssl rand -base64 24 | tr -d '\n=/+' | head -c 24)"
  MQTT_HOOK="$(openssl rand -base64 24 | tr -d '\n=/+' | head -c 24)"
  EMQX_PASS="$(openssl rand -base64 18 | tr -d '\n=/+' | head -c 18)"

  cat > .env <<EOF
POSTGRES_DB=domus
POSTGRES_USER=domus
POSTGRES_PASSWORD=${PG}
POSTGRES_PORT=5432

EMQX_MQTT_PORT=1883
EMQX_MQTT_TLS_PORT=8883
EMQX_DASHBOARD_PORT=18083
EMQX_DASHBOARD_USER=admin
EMQX_DASHBOARD_PASSWORD=${EMQX_PASS}

API_PORT=8080

JWT_ISSUER=domus
JWT_AUDIENCE=domus-app
JWT_SIGNING_KEY=${JWT}

MQTT_API_USERNAME=domus_api
MQTT_API_PASSWORD=${MQTT_API}
MQTT_HOOK_SECRET=${MQTT_HOOK}

AUTH_EXPOSE_RESET_TOKEN=false
HISTORY_RETENTION_DAYS=90
HISTORY_RETENTION_INTERVAL_HOURS=24
EOF
  echo "Criado .env com senhas aleatórias."
else
  echo ".env já existe — mantendo."
fi

cd docker
docker compose --env-file ../.env up -d --build

echo
echo "Aguardando health..."
for i in $(seq 1 40); do
  if curl -fsS "http://127.0.0.1:8080/health" >/dev/null 2>&1; then
    echo "API OK: http://$(curl -fsS ifconfig.me 2>/dev/null || echo SEU_IP):8080/health"
    docker compose --env-file ../.env ps
    exit 0
  fi
  sleep 3
done

echo "API ainda não respondeu — veja: docker compose -f docker/docker-compose.yml logs api --tail=80"
exit 1
