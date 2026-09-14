import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5049';

export const options = {
  vus: 5,
  duration: '20s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    checks: ['rate>0.99'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/api/room?page=1&pageSize=20`, {
    tags: { endpoint: 'list-rooms' },
  });

  check(response, {
    'status HTTP e 200': (result) => result.status === 200,
    'Content-Type e JSON': (result) =>
      (result.headers['Content-Type'] || '').includes('application/json'),
    'JSON possui items e totalCount': (result) => {
      try {
        const body = result.json();
        return Array.isArray(body.items) && Number.isInteger(body.totalCount);
      } catch (_) {
        return false;
      }
    },
  });

  sleep(1);
}
