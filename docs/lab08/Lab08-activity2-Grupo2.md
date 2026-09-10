# Lab 08 — Activity #2 — Load tests (REST + SignalR + MQTT)

**Group:** Grupo 2  
**Branch:** `LB-8/benchmarking-setup`  
**Date:** 2026-08-26  
**Stack:** k6 · Node `@microsoft/signalr` · Artillery · InfluxDB 1.8

Activity #1 (Eduardo) already added smoke benchmarks (`k6.test.js`, `artillery.test.yml`) and the InfluxDB service in `compose.yaml`. This report covers Activity #2 only.

## Deliverables

| File | Tool | Purpose |
|------|------|---------|
| `benchmarks/auth.test.js` | k6 | 100 concurrent `POST /api/auth/register` |
| `benchmarks/signalr.test.js` | Node + SignalR | 20 players sending `MovePlayer` |
| `benchmarks/mqtt.test.yml` (+ `mqtt-processor.cjs`) | Artillery + mqtt.js | 50 subscribers, chat + power-up, no loss |
| This report | Markdown | Metrics + how to reproduce |

InfluxDB host mapping: **`localhost:8087` → container `:8086`** (avoids clash with other local Influx instances on 8086). Database: `k6`.

---

## 1. Authentication load test (100 users)

**Command**

```bash
docker run --rm --network host \
  -v "$PWD/benchmarks:/scripts" \
  -e API_BASE=http://localhost:5049 \
  -e SUMMARY_PATH=/scripts/results/auth-summary.json \
  grafana/k6:0.54.0 run --out influxdb=http://localhost:8087/k6 /scripts/auth.test.js
```

**Scenario:** `shared-iterations` with **100 VUs / 100 iterations** (one registration each), unique emails.

### Results (run 2026-08-27)

| Metric | Value |
|--------|------:|
| Registrations OK (`201`) | **100 / 100** |
| HTTP failures | **0%** |
| `auth_errors_4xx` | **0** |
| `auth_errors_5xx` | **0** |
| `auth_error_rate` | **0%** |
| Duration avg | 4.05 s |
| Duration p95 | 5.40 s |
| Duration max | 5.72 s |
| Checks | 100% |

**Notes:** Register uses bcrypt, so a simultaneous 100-VU burst spends seconds on CPU-bound hashing. Latency is elevated under concurrency but **all requests succeeded with no 4xx/5xx**. Metrics were exported to Influx (`http_req_duration`, `auth_register_*`, tags `lab=lb8-activity2`).

---

## 2. SignalR load test (20 players)

**Command**

```bash
cd benchmarks && npm ci
INFLUX_URL=http://localhost:8087 node signalr.test.js
```

**Scenario:** register 20 players, place them in **5 rooms × 4** (API max players per room), connect to `/hubs/game`, invoke `MovePlayer` **30 times each** (600 moves). Round-trip latency measured around `invoke`.

### Results (run 2026-08-27)

| Metric | Value |
|--------|------:|
| Players | 20 |
| Rooms | 5 |
| Move ACKs | **600 / 600** |
| Errors | **0** |
| Avg latency | **2.45 ms** |
| p95 latency | **3.47 ms** |
| Congestion peak (max) | **12.23 ms** |
| Wall clock | 1.31 s |

Influx measurement: `signalr_load` (fields `avg_ms`, `p95_ms`, `peak_ms`, `acks`, `errors`).

Movement stayed synchronized with sub‑15 ms peak RTT on the local stack — no congestion collapse under the 20-player burst.

---

## 3. MQTT load test (50 devices)

**Command**

```bash
export MQTT_FRONTEND_PASSWORD=frontend-dev-pass
export MQTT_BACKEND_PASSWORD=backend-dev-pass
npx artillery run mqtt.test.yml
```

**Scenario:** 50 frontend ACL clients subscribe to `battle-tanks/LOAD-TEST/events/#`. Backend credential publishes **10 chat** + **10 power-up** messages (QoS 1). Expect **50 × 20 = 1000** deliveries; assert **zero loss**.

### Results (run 2026-08-27)

| Metric | Value |
|--------|------:|
| Subscribers | 50 |
| Messages published | 20 |
| Expected deliveries | 1000 |
| Received deliveries | **1000** |
| Lost | **0** |
| Delivery rate | **1.0000** |
| Clients complete | **50 / 50** |
| Artillery VUs failed | 0 |

Influx measurement: `mqtt_load` (`subscribers`, `lost`, `delivery_rate`, `clients_ok`).

Topics exercised (aligned with game event topic layout):

- `battle-tanks/LOAD-TEST/events/game/chat`
- `battle-tanks/LOAD-TEST/events/game/power-up`

---

## 4. InfluxDB integration

Service added in Activity #1 (`compose.yaml` → `influxdb:1.8`, `INFLUXDB_DB=k6`). Activity #2 wires all three tests into the same database:

| Source | How metrics arrive |
|--------|--------------------|
| k6 auth | `--out influxdb=http://localhost:8087/k6` |
| SignalR script | line protocol `POST /write?db=k6` → `signalr_load` |
| MQTT processor | line protocol → `mqtt_load` |

Verified measurements present after the runs:

```text
auth_register_duration, auth_register_ok, auth_error_rate,
http_req_duration, http_reqs, mqtt_load, signalr_load, ...
```

Sample queries:

```bash
curl -sG 'http://localhost:8087/query' --data-urlencode 'db=k6' \
  --data-urlencode 'q=SHOW MEASUREMENTS'
curl -sG 'http://localhost:8087/query' --data-urlencode 'db=k6' \
  --data-urlencode 'q=SELECT * FROM mqtt_load ORDER BY time DESC LIMIT 1'
curl -sG 'http://localhost:8087/query' --data-urlencode 'db=k6' \
  --data-urlencode 'q=SELECT * FROM signalr_load ORDER BY time DESC LIMIT 1'
```

---

## Rubric self-check

| Criterion | Evidence | Grade target |
|-----------|----------|--------------|
| Auth — 100 users + clear metrics | 100×201, 0×4xx/5xx, duration trends in k6 + Influx | A |
| SignalR — synchronized moves + precise latency | 600 ACKs, avg/p95/peak ms | A |
| MQTT — all clients receive events | 50/50 clients, 0 lost | A |
| InfluxDB — structured storage | `mqtt_load`, `signalr_load`, k6 HTTP series | A |

---

## How to reproduce (full stack)

```bash
docker compose up -d postgres redis emqx influxdb
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5049 \
  dotnet run --project src/BattleTanks.Api --no-launch-profile

cd benchmarks && npm ci
# then the three commands in sections 1–3
```

See also `benchmarks/README.md`.
