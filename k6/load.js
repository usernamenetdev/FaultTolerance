import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const ordersOk = new Counter('orders_ok');
const orders429 = new Counter('orders_429');
const ordersErr = new Counter('orders_err');

const magicOk = new Counter('magic_ok');
const magic429 = new Counter('magic_429');
const magicErr = new Counter('magic_err');

const BASE_URL = __ENV.BASE_URL || 'http://127.0.0.1:5167';
const SLEEP_SEC = __ENV.SLEEP ? Number(__ENV.SLEEP) : 0.2;           // было 0.1
const REQ_TIMEOUT = __ENV.REQ_TIMEOUT || '10s';                       // чтобы не висеть 60s

function makeOrderBody() { return JSON.stringify({}); }
function makeMagicBody() { return JSON.stringify({}); }

function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

let vuUserId;

export const options = {
    // Мягкий ramp-up вместо мгновенных 50 VU
    scenarios: {
        steady: {
            executor: 'ramping-vus',
            startVUs: 1,
            stages: [
                { duration: '30s', target: 5 },
                { duration: '30s', target: 10 },
                { duration: '60s', target: 10 },
            ],
            gracefulRampDown: '15s',
        },
    },

    discardResponseBodies: true,

    thresholds: {
        http_req_failed: ['rate<0.10'],
    },
};

export default function () {
    if (!vuUserId) vuUserId = uuidv4();

    const isOrders = Math.random() < 0.8;

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'X-User-Id': vuUserId,
        },
        timeout: REQ_TIMEOUT, // <-- ключевой фикс против 60s зависаний
    };

    if (isOrders) {
        const res = http.post(`${BASE_URL}/orders`, makeOrderBody(), { ...params, tags: { name: 'POST /orders' } });

        if (res.status >= 200 && res.status < 300) ordersOk.add(1);
        else if (res.status === 429) orders429.add(1);
        else ordersErr.add(1);

        check(res, { 'orders: status is 2xx or 429': (r) => (r.status >= 200 && r.status < 300) || r.status === 429 });
    } else {
        const res = http.post(`${BASE_URL}/magic-link`, makeMagicBody(), { ...params, tags: { name: 'POST /magic-link' } });

        if (res.status >= 200 && res.status < 300) magicOk.add(1);
        else if (res.status === 429) magic429.add(1);
        else magicErr.add(1);

        check(res, { 'magic-link: status is 2xx or 429': (r) => (r.status >= 200 && r.status < 300) || r.status === 429 });
    }

    sleep(SLEEP_SEC);
}