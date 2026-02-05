import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';

const ordersOk = new Counter('orders_ok');
const orders429 = new Counter('orders_429');
const ordersErr = new Counter('orders_err');

const magicOk = new Counter('magic_ok');
const magic429 = new Counter('magic_429');
const magicErr = new Counter('magic_err');

// Настройки через env
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5178'; // apigateway
const SLEEP_SEC = __ENV.SLEEP ? Number(__ENV.SLEEP) : 0.1;

// Если твой /orders требует тело — оставь как есть.
// Если не требует — можно заменить на {}.
function makeOrderBody() {
  return JSON.stringify({
    amount: 100.0,
    currency: 'USD',
    fingerprint: 'fp-demo'
  });
}

// Если твой /magic-link требует тело — подставь нужные поля.
// Если не требует — оставь {}.
function makeMagicBody() {
  return JSON.stringify({});
}

// UUID v4 (GUID) генератор без внешних библиотек
function uuidv4() {
  // RFC4122-ish; для нагрузки более чем достаточно
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// У каждого VU свой JS runtime, поэтому переменная будет "стабильной" для VU
let vuUserId;

export const options = {
  vus: __ENV.VUS ? parseInt(__ENV.VUS, 10) : 50,
  duration: __ENV.DURATION || '2m',
  discardResponseBodies: true,

  thresholds: {
    http_req_failed: ['rate<0.05'], // подстрой под свои ожидания
  },
};

export default function () {
  if (!vuUserId) vuUserId = uuidv4();

  // 80/20 распределение
  const isOrders = Math.random() < 0.8;

  const headers = {
    'Content-Type': 'application/json',
    'X-User-Id': vuUserId,
  };

  if (isOrders) {
    const res = http.post(`${BASE_URL}/orders`, makeOrderBody(), {
      headers,
      tags: { name: 'POST /orders' },
    });

    if (res.status >= 200 && res.status < 300) ordersOk.add(1);
    else if (res.status === 429) orders429.add(1);
    else ordersErr.add(1);

    check(res, {
      'orders: status is 2xx or 429': (r) => (r.status >= 200 && r.status < 300) || r.status === 429,
    });
  } else {
    const res = http.post(`${BASE_URL}/magic-link`, makeMagicBody(), {
      headers,
      tags: { name: 'POST /magic-link' },
    });

    if (res.status >= 200 && res.status < 300) magicOk.add(1);
    else if (res.status === 429) magic429.add(1);
    else magicErr.add(1);

    check(res, {
      'magic-link: status is 2xx or 429': (r) => (r.status >= 200 && r.status < 300) || r.status === 429,
    });
  }

  sleep(SLEEP_SEC);
}