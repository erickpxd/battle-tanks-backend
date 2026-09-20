import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '10s', target: 20 }, // Ramp-up to 20 users
    { duration: '20s', target: 20 }, // Stay at 20 users for 20 seconds
    { duration: '10s', target: 0 },  // Ramp-down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests should be below 500ms
  },
};

export default function () {
  // Testando o endpoint público para listar as salas
  const res = http.get('http://localhost:5049/api/room');
  
  check(res, {
    'status is 200': (r) => r.status === 200,
    'is json': (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/json'),
  });

  sleep(1);
}
