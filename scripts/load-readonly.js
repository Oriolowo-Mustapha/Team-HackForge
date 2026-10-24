// k6: bounded, read-only local full-stack benchmark. Never target a hosted deployment.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const target = (__ENV.FARMHAND_LOAD_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');
if (!/^http:\/\/(127\.0\.0\.1|localhost):\d+$/.test(target)) {
  throw new Error('Only an explicit loopback HTTP target is permitted.');
}
const vus = Number(__ENV.FARMHAND_LOAD_VUS || 10);
if (![10, 25, 50].includes(vus)) throw new Error('Use 10, 25, or 50 virtual users.');
const unexpected = new Rate('unexpected_failure');
const throttled = new Rate('rate_limited');
const duration = new Trend('read_duration', true);
const reads = new Counter('read_requests');
export const options = {
  scenarios: { read_burst: { executor: 'per-vu-iterations', vus, iterations: 2, maxDuration: '45s' } },
  maxRedirects: 0,
  thresholds: { unexpected_failure: ['rate==0'] },
  summaryTrendStats: ['avg', 'min', 'med', 'p(95)', 'max'],
};

export function setup() {
  const info = http.get(`${target}/api/bff/system/info`, { redirects: 0, tags: { name: 'setup_mode' } });
  if (info.status !== 200 || info.json().demoMode !== true || info.json().paymentsMode !== 'Simulated') {
    throw new Error('Benchmark requires the explicitly labelled local demo with Simulated payments.');
  }
  if (!__ENV.FARMHAND_LOAD_PASSWORD) throw new Error('Set the isolated demo FARMHAND_LOAD_PASSWORD; it is never logged.');
  const response = http.post(`${target}/api/session/login`, JSON.stringify({
    email: __ENV.FARMHAND_LOAD_EMAIL || 'farmer@farmhand.ai', password: __ENV.FARMHAND_LOAD_PASSWORD,
  }), { headers: { 'Content-Type': 'application/json', 'x-farmhand-csrf': '1', Origin: target }, redirects: 0, tags: { name: 'setup_login' } });
  if (response.status !== 200) throw new Error(`Local login failed (${response.status}).`);
  const cookie = Object.keys(response.cookies).filter(name => name.startsWith('fh_'))
    .map(name => `${name}=${response.cookies[name][0].value}`).join('; ');
  const farms = http.get(`${target}/api/bff/farms`, { headers: { Cookie: cookie }, redirects: 0, tags: { name: 'setup_farms' } });
  if (farms.status !== 200 || !farms.json()[0]?.id) throw new Error('An isolated seeded farm is required.');
  return { cookie, farmId: farms.json()[0].id };
}

export default function (data) {
  // 6 reads per VU across 2 iterations: 60/150/300 requests per scenario.
  const endpoints = [
    `farms/${data.farmId}/overview`,
    `observations?farmId=${data.farmId}&page=1&pageSize=20`,
    `incidents?farmId=${data.farmId}&page=1&pageSize=20`,
  ];
  for (let index = 0; index < endpoints.length; index++) {
    const response = http.get(`${target}/api/bff/${endpoints[index]}`, {
      headers: { Cookie: data.cookie }, timeout: '15s', redirects: 0,
      tags: { name: ['dashboard', 'reports', 'cases'][index] },
    });
    throttled.add(response.status === 429);
    duration.add(response.timings.duration);
    reads.add(1);
    unexpected.add(response.status !== 200 && response.status !== 429);
    check(response, { 'authenticated read succeeded': r => r.status === 200 });
    sleep(0.15);
  }
}

export function handleSummary(data) {
  // Summaries contain aggregate metrics only, never response bodies or credentials.
  return { [__ENV.FARMHAND_LOAD_RESULT || `load-${vus}.json`]: JSON.stringify(data, null, 2) };
}
