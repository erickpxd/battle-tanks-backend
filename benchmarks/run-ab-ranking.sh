#!/usr/bin/env bash
# LB-9 Activity #3 — Apache Bench against GET /api/ranking/top
set -euo pipefail

API_BASE="${API_BASE:-http://localhost:5049}"
REQUESTS="${REQUESTS:-2000}"
CONCURRENCY="${CONCURRENCY:-50}"
OUT_DIR="$(cd "$(dirname "$0")" && pwd)/results"
mkdir -p "$OUT_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
OUT_FILE="$OUT_DIR/ab-ranking-$STAMP.txt"

echo "Apache Bench → $API_BASE/api/ranking/top"
echo "  -n $REQUESTS  -c $CONCURRENCY"
ab -n "$REQUESTS" -c "$CONCURRENCY" -g "$OUT_DIR/ab-ranking-$STAMP.tsv" \
  "$API_BASE/api/ranking/top?take=10" | tee "$OUT_FILE"

echo "Saved: $OUT_FILE"
