# LB-8 Activity #2 — load tests

Scripts for authentication (k6), SignalR (Node), and MQTT (Artillery) with
metrics pushed to InfluxDB 1.8 (`compose.yaml` service `influxdb`, host port **8087**).

## Prerequisites

```bash
docker compose up -d postgres redis emqx influxdb
dotnet run --project src/BattleTanks.Api --no-launch-profile
# ASPNETCORE_URLS=http://localhost:5049
```

```bash
cd benchmarks && npm ci
```

k6 (auth test) via Docker if not installed locally:

```bash
docker pull grafana/k6:0.54.0
```

## Run

```bash
# Auth — 100 concurrent registrations
docker run --rm --network host \
  -v "$PWD:/scripts" \
  -e API_BASE=http://localhost:5049 \
  -e SUMMARY_PATH=/scripts/results/auth-summary.json \
  grafana/k6:0.54.0 run --out influxdb=http://localhost:8087/k6 /scripts/auth.test.js

# SignalR — 20 players / MovePlayer latency
INFLUX_URL=http://localhost:8087 node signalr.test.js

# MQTT — 50 subscribers, chat + power-up, zero loss
export MQTT_FRONTEND_PASSWORD=frontend-dev-pass
export MQTT_BACKEND_PASSWORD=backend-dev-pass
npx artillery run mqtt.test.yml
```

JSON reports land in `benchmarks/results/` (gitignored). InfluxDB queries:

```bash
curl -sG 'http://localhost:8087/query' --data-urlencode 'db=k6' --data-urlencode 'q=SHOW MEASUREMENTS'
curl -sG 'http://localhost:8087/query' --data-urlencode 'db=k6' --data-urlencode 'q=SELECT * FROM signalr_load ORDER BY time DESC LIMIT 1'
curl -sG 'http://localhost:8087/query' --data-urlencode 'db=k6' --data-urlencode 'q=SELECT * FROM mqtt_load ORDER BY time DESC LIMIT 1'
```

Host port **8087** avoids clashing with other local InfluxDB instances on 8086.

---

## LB-9 Activity #3 — alternative tools

| File | Tool |
|------|------|
| `login.test.jmx` | JMeter — 1000 concurrent `POST /api/auth/login` |
| `ranking-benchmark.scala` | Gatling — ranking + SignalR hub + MQTT config probes |
| `run-ab-ranking.sh` | Apache Bench — `GET /api/ranking/top` |
| `redis-latency-monitor.sh` | Redis MONITOR/latency → Influx; Grafana alert > 50ms |
| `run-activity3.sh` | Docker helpers for JMeter + Gatling + ab + Redis |

```bash
docker compose up -d postgres redis emqx influxdb grafana
# Grafana UI: http://localhost:3001 (admin/admin) — dashboard Lab 09 - Redis Latency
ASPNETCORE_URLS=http://0.0.0.0:5049 dotnet run --project src/BattleTanks.Api --no-launch-profile

cd benchmarks
./run-activity3.sh
# or individually:
#   ./run-ab-ranking.sh
#   ./redis-latency-monitor.sh
```

