import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';
import { SharedArray } from 'k6/data';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.4/index.js';

/**
 * LB-8 Activity #2 — Authentication load test
 *
 * Simulates 100 simultaneous registrations against POST /api/auth/register,
 * measuring latency and counting 4xx / 5xx responses.
 *
 * Run (with InfluxDB from compose):
 *   k6 run --out influxdb=http://localhost:8087/k6 benchmarks/auth.test.js
 *
 * Or via Docker:
 *   docker run --rm --network host grafana/k6 run \
 *     --out influxdb=http://localhost:8087/k6 \
 *     - < benchmarks/auth.test.js
 */

const API_BASE = __ENV.API_BASE || 'http://localhost:5049';
const RUN_ID = __ENV.RUN_ID || `auth-${Date.now()}`;

const errors4xx = new Counter('auth_errors_4xx');
const errors5xx = new Counter('auth_errors_5xx');
const registerOk = new Counter('auth_register_ok');
const registerDuration = new Trend('auth_register_duration', true);
const errorRate = new Rate('auth_error_rate');

export const options = {
  tags: {
    test: 'auth_register',
    lab: 'lb8-activity2',
    run_id: RUN_ID,
  },
  scenarios: {
    // Exactly 100 registrations fired as a concurrent burst.
    register_100_users: {
      executor: 'shared-iterations',
      vus: 100,
      iterations: 100,
      maxDuration: '3m',
    },
  },
  thresholds: {
    // Register hashes passwords (bcrypt); under a 100-VU burst p95 is typically multi-second.
    http_req_duration: ['p(95)<10000', 'p(99)<15000'],
    auth_error_rate: ['rate<0.15'],
    auth_errors_5xx: ['count<5'],
    checks: ['rate>0.85'],
  },
};

const passwords = new SharedArray('passwords', () => ['LoadTest!123']);

export default function () {
  const vu = __VU;
  const iter = __ITER;
  const suffix = `${RUN_ID}-${vu}-${iter}-${Date.now()}`;
  const payload = JSON.stringify({
    username: `loaduser_${suffix}`.slice(0, 40),
    email: `loaduser_${suffix}@battletanks.load`.toLowerCase(),
    password: passwords[0],
  });

  const started = Date.now();
  const res = http.post(`${API_BASE}/api/auth/register`, payload, {
    headers: { 'Content-Type': 'application/json' },
    tags: { endpoint: 'register' },
  });
  const elapsed = Date.now() - started;
  registerDuration.add(elapsed);

  const status = res.status;
  const class4xx = status >= 400 && status < 500;
  const class5xx = status >= 500;
  const ok = status === 201;

  if (class4xx) errors4xx.add(1);
  if (class5xx) errors5xx.add(1);
  if (ok) registerOk.add(1);
  errorRate.add(!(ok || status === 409)); // 409 duplicate email is acceptable under retries

  check(res, {
    'register status is 201 or 409': (r) => r.status === 201 || r.status === 409,
    'register not 5xx': (r) => r.status < 500,
    'register latency under 10s': () => elapsed < 10000,
  });

  // Tiny jitter so the burst is not a perfect lock-step.
  sleep(0.01 * (vu % 5));
}

export function handleSummary(data) {
  const path = __ENV.SUMMARY_PATH || 'benchmarks/results/auth-summary.json';
  return {
    [path]: JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
