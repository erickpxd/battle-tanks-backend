#!/usr/bin/env bash
# Helpers to run LB-9 Activity #3 tools via Docker when not installed locally.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BENCH="$ROOT/benchmarks"
API_BASE="${API_BASE:-http://localhost:5049}"

mkdir -p "$BENCH/results" "$BENCH/gatling/user-files/simulations/battletanks" "$BENCH/gatling/results"

echo "== JMeter login.test.jmx (1000 users) =="
# --network host so the container reaches the API on localhost:5049 (Linux).
docker run --rm --network host \
  -v "$BENCH:/bench" \
  justb4/jmeter:5.5 \
  -n -t /bench/login.test.jmx \
  -JAPI_BASE="$API_BASE" \
  -l /bench/results/jmeter-login.jtl \
  -e -o /bench/results/jmeter-login-report \
  | tee "$BENCH/results/jmeter-login-console.txt"

echo "== Gatling ranking-benchmark.scala =="
mkdir -p "$BENCH/gatling/user-files/simulations/battletanks" "$BENCH/gatling/results"
cp -f "$BENCH/ranking-benchmark.scala" \
  "$BENCH/gatling/user-files/simulations/battletanks/RankingBenchmark.scala"
# Mount only user-files + results — do NOT overlay /opt/gatling (wipes bin/).
docker run --rm --network host \
  -v "$BENCH/gatling/user-files:/opt/gatling/user-files" \
  -v "$BENCH/gatling/results:/opt/gatling/results" \
  -e JAVA_OPTS="-DapiBase=$API_BASE" \
  denvazh/gatling:3.0.3 \
  -s battletanks.RankingBenchmark \
  | tee "$BENCH/results/gatling-console.txt" || true

echo "== Apache Bench ranking =="
API_BASE="$API_BASE" "$BENCH/run-ab-ranking.sh"

echo "== Redis latency → Influx =="
bash "$BENCH/redis-latency-monitor.sh"

echo "All Activity #3 runners finished. See benchmarks/results/"
