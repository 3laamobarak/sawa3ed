// Run against a disposable/staging environment: k6 run -e BASE_URL=... -e ACCESS_TOKEN=... benchmarks/smoke.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: { http_req_failed: ['rate<0.01'], http_req_duration: ['p(95)<500'] },
};

export default function () {
  const baseUrl = __ENV.BASE_URL || 'http://localhost:5080';
  const token = __ENV.ACCESS_TOKEN;
  const response = token
    ? http.get(`${baseUrl}/api/v1/auth/me`, { headers: { Authorization: `Bearer ${token}` } })
    : http.get(`${baseUrl}/health/ready`);
  check(response, { 'returns 200': r => r.status === 200 });
  sleep(1);
}
