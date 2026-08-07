#!/bin/sh
set -eu

TEMPLATE="/opt/emqx/etc/emqx.conf.template"
TARGET="/opt/emqx/etc/emqx.conf"

if [ -z "${DOMUS_MQTT_HOOK_SECRET:-}" ]; then
  echo "ERROR: DOMUS_MQTT_HOOK_SECRET is required" >&2
  exit 1
fi

if [ ! -f "$TEMPLATE" ]; then
  echo "ERROR: missing $TEMPLATE" >&2
  exit 1
fi

# EMQX does not expand custom OS env vars inside mounted HOCON.
# Substitute the Domus hook secret before starting the broker.
sed "s|__DOMUS_MQTT_HOOK_SECRET__|${DOMUS_MQTT_HOOK_SECRET}|g" "$TEMPLATE" > "$TARGET"

exec /usr/bin/docker-entrypoint.sh "$@"
