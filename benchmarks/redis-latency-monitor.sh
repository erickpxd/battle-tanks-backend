#!/usr/bin/env bash
# LB-9 Activity #3 — sample Redis command latency and push to InfluxDB.
# Samples run *inside* the Redis container (avoids docker-exec RTT inflation).
# Also captures a short MONITOR / SLOWLOG window. Grafana alerts when avg > 50ms.
set -euo pipefail

INFLUX_URL="${INFLUX_URL:-http://localhost:8087}"
INFLUX_DB="${INFLUX_DB:-k6}"
SAMPLES="${SAMPLES:-30}"
INTERVAL_SEC="${INTERVAL_SEC:-1}"
MONITOR_SEC="${MONITOR_SEC:-5}"

redis() {
  docker exec battle-tanks-redis redis-cli "$@"
}

echo "Sampling Redis latency ($SAMPLES × ${INTERVAL_SEC}s) → Influx $INFLUX_URL/$INFLUX_DB"
echo "(measurements taken inside container via redis-cli --latency-history / TIME)"

# Brief MONITOR capture (assignment asks for redis-cli MONITOR)
echo "=== redis-cli MONITOR (${MONITOR_SEC}s sample) ==="
timeout "${MONITOR_SEC}" docker exec battle-tanks-redis redis-cli MONITOR 2>/dev/null \
  | head -n 40 || true
echo "=== end MONITOR ==="

for ((i = 1; i <= SAMPLES; i++)); do
  # Round-trip of PING measured inside the container (µs → ms)
  ping_us=$(docker exec battle-tanks-redis sh -c 'start=$(date +%s%N); redis-cli PING >/dev/null; end=$(date +%s%N); echo $(( (end - start) / 1000 ))')
  ranking_us=$(docker exec battle-tanks-redis sh -c 'start=$(date +%s%N); redis-cli ZREVRANGE battle-tanks:ranking:global 0 9 WITHSCORES >/dev/null; end=$(date +%s%N); echo $(( (end - start) / 1000 ))')

  latency_ms=$(awk -v u="$ping_us" 'BEGIN { printf "%.3f", u / 1000 }')
  ranking_ms=$(awk -v u="$ranking_us" 'BEGIN { printf "%.3f", u / 1000 }')

  line="redis_op_latency,lab=lb9-activity3,op=ping latency_ms=${latency_ms}"
  line2="redis_op_latency,lab=lb9-activity3,op=zrevrange_ranking latency_ms=${ranking_ms}"
  curl -sS -XPOST "${INFLUX_URL}/write?db=${INFLUX_DB}&precision=ms" \
    --data-binary "${line}
${line2}
" >/dev/null

  printf '[%02d/%02d] ping=%sms ranking=%sms\n' "$i" "$SAMPLES" "$latency_ms" "$ranking_ms"
  sleep "$INTERVAL_SEC"
done

echo "SLOWLOG (slowest recent Redis ops):"
redis SLOWLOG GET 10 || true
echo "Done. Query: SELECT * FROM redis_op_latency ORDER BY time DESC LIMIT 5"
echo "Grafana: http://localhost:3001/d/lab09-redis (alert: mean latency > 50ms)"
