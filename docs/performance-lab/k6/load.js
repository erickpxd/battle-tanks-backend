import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5049';

export const options = {
  stages: [
    { duration: '10s', target: 5 },
    { duration: '20s', target: 15 },
    { duration: '15s', target: 25 },
    { duration: '10s', target: 5 },
    { duration: '5s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/api/room?page=1&pageSize=20`, {
    tags: { endpoint: 'list-rooms-progressive' },
  });

  check(response, {
    'status HTTP e 200': (result) => result.status === 200,
    'resposta paginada valida': (result) => {
      try {
        const body = result.json();
        return (
          (result.headers['Content-Type'] || '').includes('application/json') &&
          Array.isArray(body.items) &&
          Number.isInteger(body.totalCount)
        );
      } catch (_) {
        return false;
      }
    },
  });

  sleep(1);
}
