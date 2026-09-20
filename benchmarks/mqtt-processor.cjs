/**
 * Artillery processor for LB-8 Activity #2 MQTT load test.
 *
 * Connects 50 frontend subscribers to chat + power-up topics, publishes a
 * known set of messages with the backend credential, asserts zero loss, and
 * writes metrics to InfluxDB 1.8.
 */

const mqtt = require('mqtt');
const fs = require('fs');
const path = require('path');

const MQTT_URL = process.env.MQTT_URL || 'mqtt://localhost:1883';
const ROOM_ID = (process.env.MQTT_ROOM_ID || 'LOAD-TEST').toUpperCase();
const SUBSCRIBER_COUNT = Number(process.env.MQTT_SUBSCRIBERS || 50);
const MESSAGES_PER_TOPIC = Number(process.env.MQTT_MESSAGES_PER_TOPIC || 10);
const INFLUX_URL = process.env.INFLUX_URL || 'http://localhost:8087';
const INFLUX_DB = process.env.INFLUX_DB || 'k6';
const RUN_ID = process.env.RUN_ID || `mqtt-${Date.now()}`;

const FRONTEND_USER = process.env.MQTT_FRONTEND_USERNAME || 'battle-tanks-frontend';
const FRONTEND_PASS = process.env.MQTT_FRONTEND_PASSWORD || 'frontend-dev-pass';
const BACKEND_USER = process.env.MQTT_BACKEND_USERNAME || 'battle-tanks-backend';
const BACKEND_PASS = process.env.MQTT_BACKEND_PASSWORD || 'backend-dev-pass';

const CHAT_TOPIC = `battle-tanks/${ROOM_ID}/events/game/chat`;
const POWERUP_TOPIC = `battle-tanks/${ROOM_ID}/events/game/power-up`;
const SUBSCRIBE_FILTER = `battle-tanks/${ROOM_ID}/events/#`;

const EXPECTED_PER_SUBSCRIBER = MESSAGES_PER_TOPIC * 2; // chat + power-up
const EXPECTED_TOTAL = SUBSCRIBER_COUNT * EXPECTED_PER_SUBSCRIBER;

/**
 * Artillery entrypoint — one VU runs the full fan-out validation.
 * Prefer async/Promise style (Artillery 2.x); keep optional done for older runners.
 */
async function runMqttLoadTest(context, ee, done) {
  try {
    const report = await executeLoadTest();
    context.vars.mqttReport = report;
    if (report.lostMessages > 0 || report.deliveryRate < 1) {
      const err = new Error(`MQTT message loss detected: lost=${report.lostMessages} rate=${report.deliveryRate}`);
      if (typeof done === 'function') return done(err);
      throw err;
    }
    if (typeof done === 'function') return done();
    return;
  } catch (error) {
    const err = error instanceof Error ? error : new Error(String(error));
    if (typeof done === 'function') return done(err);
    throw err;
  }
}

async function executeLoadTest() {
  const receivedByClient = Array.from({ length: SUBSCRIBER_COUNT }, () => new Set());
  const subscribers = [];

  for (let i = 0; i < SUBSCRIBER_COUNT; i += 1) {
    const client = await connectClient({
      username: FRONTEND_USER,
      password: FRONTEND_PASS,
      clientId: `lb8-mqtt-sub-${RUN_ID}-${i}`,
    });
    await subscribe(client, SUBSCRIBE_FILTER);
    client.on('message', (topic, payload) => {
      if (topic !== CHAT_TOPIC && topic !== POWERUP_TOPIC) return;
      try {
        const body = JSON.parse(payload.toString('utf8'));
        if (body && body.messageId) {
          receivedByClient[i].add(`${topic}|${body.messageId}`);
        }
      } catch {
        // ignore malformed payloads
      }
    });
    subscribers.push(client);
  }

  // Brief settle so EMQX finishes ACL/subscribe fan-out.
  await delay(500);

  const publisher = await connectClient({
    username: BACKEND_USER,
    password: BACKEND_PASS,
    clientId: `lb8-mqtt-pub-${RUN_ID}`,
  });

  const publishedIds = [];
  for (let n = 0; n < MESSAGES_PER_TOPIC; n += 1) {
    const chatId = `chat-${n}`;
    const powerId = `power-${n}`;
    await publishJson(publisher, CHAT_TOPIC, {
      messageId: chatId,
      eventType: 'chat',
      roomId: ROOM_ID,
      text: `load-chat-${n}`,
      runId: RUN_ID,
    });
    await publishJson(publisher, POWERUP_TOPIC, {
      messageId: powerId,
      eventType: 'power-up',
      roomId: ROOM_ID,
      power: 'shield',
      runId: RUN_ID,
    });
    publishedIds.push(`${CHAT_TOPIC}|${chatId}`, `${POWERUP_TOPIC}|${powerId}`);
    await delay(20);
  }

  // Wait for delivery across all subscribers.
  const deadline = Date.now() + 10_000;
  while (Date.now() < deadline) {
    const total = receivedByClient.reduce((sum, set) => sum + set.size, 0);
    if (total >= EXPECTED_TOTAL) break;
    await delay(100);
  }

  await endClient(publisher);
  await Promise.all(subscribers.map(endClient));

  let receivedTotal = 0;
  let clientsComplete = 0;
  for (const set of receivedByClient) {
    receivedTotal += set.size;
    if (set.size >= EXPECTED_PER_SUBSCRIBER) clientsComplete += 1;
  }

  const lostMessages = EXPECTED_TOTAL - receivedTotal;
  const deliveryRate = EXPECTED_TOTAL === 0 ? 0 : receivedTotal / EXPECTED_TOTAL;
  const report = {
    runId: RUN_ID,
    generatedAt: new Date().toISOString(),
    roomId: ROOM_ID,
    subscriberCount: SUBSCRIBER_COUNT,
    topics: [CHAT_TOPIC, POWERUP_TOPIC],
    messagesPublished: publishedIds.length,
    expectedDeliveries: EXPECTED_TOTAL,
    receivedDeliveries: receivedTotal,
    clientsComplete,
    lostMessages: Math.max(0, lostMessages),
    deliveryRate: Number(deliveryRate.toFixed(4)),
  };

  const resultsDir = path.join(__dirname, 'results');
  fs.mkdirSync(resultsDir, { recursive: true });
  const outPath = path.join(resultsDir, `mqtt-${RUN_ID}.json`);
  fs.writeFileSync(outPath, JSON.stringify(report, null, 2));
  console.log(`MQTT report: ${outPath}`);
  console.table({
    subscribers: SUBSCRIBER_COUNT,
    published: publishedIds.length,
    expected: EXPECTED_TOTAL,
    received: receivedTotal,
    lost: report.lostMessages,
    delivery_rate: report.deliveryRate,
    clients_ok: clientsComplete,
  });

  await writeInflux(report);
  return report;
}

function connectClient({ username, password, clientId }) {
  return new Promise((resolve, reject) => {
    const client = mqtt.connect(MQTT_URL, {
      username,
      password,
      clientId,
      clean: true,
      protocolVersion: 4,
      reconnectPeriod: 0,
      connectTimeout: 10_000,
    });
    const onError = (error) => {
      cleanup();
      reject(error);
    };
    const onConnect = () => {
      cleanup();
      resolve(client);
    };
    const cleanup = () => {
      client.off('connect', onConnect);
      client.off('error', onError);
    };
    client.once('connect', onConnect);
    client.once('error', onError);
  });
}

function subscribe(client, topic) {
  return new Promise((resolve, reject) => {
    client.subscribe(topic, { qos: 1 }, (error) => {
      if (error) reject(error);
      else resolve();
    });
  });
}

function publishJson(client, topic, payload) {
  return new Promise((resolve, reject) => {
    client.publish(topic, JSON.stringify(payload), { qos: 1 }, (error) => {
      if (error) reject(error);
      else resolve();
    });
  });
}

function endClient(client) {
  return new Promise((resolve) => {
    try {
      client.end(true, {}, () => resolve());
    } catch {
      resolve();
    }
  });
}

async function writeInflux(report) {
  const line =
    `mqtt_load,run_id=${esc(report.runId)},lab=lb8-activity2 ` +
    `subscribers=${report.subscriberCount}i,published=${report.messagesPublished}i,` +
    `expected=${report.expectedDeliveries}i,received=${report.receivedDeliveries}i,` +
    `lost=${report.lostMessages}i,delivery_rate=${report.deliveryRate},clients_ok=${report.clientsComplete}i`;
  const url = `${INFLUX_URL}/write?db=${encodeURIComponent(INFLUX_DB)}&precision=ms`;
  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'text/plain' },
      body: `${line}\n`,
    });
    if (!res.ok) {
      console.warn(`InfluxDB write failed (${res.status}): ${await res.text()}`);
    } else {
      console.log(`InfluxDB: wrote mqtt_load to ${INFLUX_DB}`);
    }
  } catch (error) {
    console.warn(`InfluxDB unreachable: ${error instanceof Error ? error.message : error}`);
  }
}

function esc(value) {
  return String(value).replace(/[,\s=]/g, '_');
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

module.exports = {
  runMqttLoadTest,
};
