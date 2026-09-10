#!/usr/bin/env node
/**
 * LB-8 Activity #2 — SignalR load test
 *
 * Spins up 20 authenticated players across rooms of 4 (API max), each sending
 * MovePlayer events. Records average / peak round-trip latency and writes
 * samples to InfluxDB 1.8 (compose service on host :8087).
 *
 *   cd benchmarks && npm ci && node signalr.test.js
 */

import { performance } from 'node:perf_hooks';
import { mkdir, writeFile } from 'node:fs/promises';
import { HubConnectionBuilder, LogLevel, HttpTransportType } from '@microsoft/signalr';

const API_BASE = process.env.API_BASE ?? 'http://localhost:5049';
const HUB_URL = process.env.SIGNALR_HUB_URL ?? `${API_BASE}/hubs/game`;
const PLAYER_COUNT = Number(process.env.PLAYER_COUNT ?? 20);
const ROOM_CAPACITY = Number(process.env.ROOM_CAPACITY ?? 4);
const MOVES_PER_PLAYER = Number(process.env.MOVES_PER_PLAYER ?? 30);
const MOVE_INTERVAL_MS = Number(process.env.MOVE_INTERVAL_MS ?? 40);
const INFLUX_URL = process.env.INFLUX_URL ?? 'http://localhost:8087';
const INFLUX_DB = process.env.INFLUX_DB ?? 'k6';
const RUN_ID = process.env.RUN_ID ?? `signalr-${Date.now()}`;
const directions = ['up', 'right', 'down', 'left'];

async function main() {
  console.log(`LB-8 SignalR load test — ${PLAYER_COUNT} players × ${MOVES_PER_PLAYER} moves`);
  console.log(`API=${API_BASE} hub=${HUB_URL} influx=${INFLUX_URL}/${INFLUX_DB}`);

  const players = [];
  for (let i = 0; i < PLAYER_COUNT; i += 1) {
    players.push(await registerPlayer(i));
  }

  const rooms = [];
  for (let offset = 0; offset < players.length; offset += ROOM_CAPACITY) {
    const cohort = players.slice(offset, offset + ROOM_CAPACITY);
    const roomCode = await createRoom(cohort[0].token);
    for (const player of cohort.slice(1)) {
      await joinRoom(player.token, roomCode);
    }
    rooms.push({ roomCode, players: cohort });
    console.log(`Room ${roomCode}: ${cohort.length} players`);
  }

  const latencies = [];
  const errors = [];
  const connections = [];

  for (const room of rooms) {
    for (const [indexInRoom, player] of room.players.entries()) {
      const connection = new HubConnectionBuilder()
        .withUrl(HUB_URL, {
          accessTokenFactory: () => player.token,
          transport: HttpTransportType.WebSockets,
        })
        .configureLogging(LogLevel.Error)
        .build();

      connection.on('ReceivePlayerMove', () => {});
      connection.on('ReceivePlayersUpdated', () => {});
      connection.on('ReceiveLocalPlayerId', () => {});
      connection.on('ReceiveChatHistory', () => {});
      connection.on('ReceiveGameEventHistory', () => {});

      await connection.start();
      await connection.invoke('JoinRoom', room.roomCode);
      connections.push({
        connection,
        roomCode: room.roomCode,
        index: connections.length,
        indexInRoom,
      });
    }
  }

  const wallStart = performance.now();
  for (let move = 0; move < MOVES_PER_PLAYER; move += 1) {
    await Promise.all(
      connections.map(async ({ connection, index }) => {
        const request = {
          x: (move + index) % 20,
          y: (move * 2 + index) % 15,
          direction: directions[move % directions.length],
          eventId: `${RUN_ID}-p${index}-m${move}`,
          clientSentAtUnixMs: Date.now(),
        };
        const t0 = performance.now();
        try {
          await connection.invoke('MovePlayer', request);
          latencies.push(performance.now() - t0);
        } catch (error) {
          errors.push(error instanceof Error ? error.message : String(error));
        }
      }),
    );
    await delay(MOVE_INTERVAL_MS);
  }
  const wallMs = performance.now() - wallStart;

  await Promise.allSettled(connections.map(({ connection }) => connection.stop()));

  const stats = summarize(latencies);
  const report = {
    runId: RUN_ID,
    generatedAt: new Date().toISOString(),
    playerCount: PLAYER_COUNT,
    roomCount: rooms.length,
    roomCapacity: ROOM_CAPACITY,
    movesPerPlayer: MOVES_PER_PLAYER,
    totalMovesAttempted: PLAYER_COUNT * MOVES_PER_PLAYER,
    totalAcks: latencies.length,
    errorCount: errors.length,
    wallClockMs: Number(wallMs.toFixed(2)),
    latencyMs: stats,
    congestionPeakMs: stats.max,
    sampleErrors: errors.slice(0, 5),
  };

  await mkdir('results', { recursive: true });
  const outPath = `results/signalr-${RUN_ID}.json`;
  await writeFile(outPath, JSON.stringify(report, null, 2));
  await writeInflux(report);

  console.table({
    players: PLAYER_COUNT,
    rooms: rooms.length,
    acks: latencies.length,
    errors: errors.length,
    avg_ms: stats.avg,
    p95_ms: stats.p95,
    peak_ms: stats.max,
    wall_s: Number((wallMs / 1000).toFixed(2)),
  });
  console.log(`Report: ${outPath}`);
  if (errors.length > latencies.length * 0.1) {
    process.exitCode = 1;
  }
}

async function registerPlayer(index) {
  const stamp = `${Date.now()}-${index}-${Math.random().toString(16).slice(2, 8)}`;
  const body = {
    username: `sig_${stamp}`.slice(0, 40),
    email: `sig_${stamp}@battletanks.load`,
    password: 'LoadTest!123',
  };
  const res = await fetch(`${API_BASE}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(`register failed (${res.status}): ${await res.text()}`);
  }
  const json = await res.json();
  return { token: json.token, playerId: json.playerId, username: json.username };
}

async function createRoom(token) {
  const res = await fetch(`${API_BASE}/api/room`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ mapSelected: 'default', region: 'sa-east-1' }),
  });
  if (!res.ok) {
    throw new Error(`create room failed (${res.status}): ${await res.text()}`);
  }
  const json = await res.json();
  return json.roomCode;
}

async function joinRoom(token, roomCode) {
  const res = await fetch(`${API_BASE}/api/room/join`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ roomCode }),
  });
  if (!res.ok) {
    throw new Error(`join room failed (${res.status}): ${await res.text()}`);
  }
}

function summarize(values) {
  if (values.length === 0) {
    return { avg: 0, p50: 0, p95: 0, max: 0, min: 0 };
  }
  const sorted = [...values].sort((a, b) => a - b);
  const avg = values.reduce((a, b) => a + b, 0) / values.length;
  const pct = (p) => sorted[Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length))];
  return {
    avg: Number(avg.toFixed(2)),
    p50: Number(pct(50).toFixed(2)),
    p95: Number(pct(95).toFixed(2)),
    max: Number(sorted[sorted.length - 1].toFixed(2)),
    min: Number(sorted[0].toFixed(2)),
  };
}

async function writeInflux(report) {
  const line =
    `signalr_load,run_id=${esc(report.runId)},lab=lb8-activity2 ` +
    `player_count=${report.playerCount}i,acks=${report.totalAcks}i,errors=${report.errorCount}i,` +
    `avg_ms=${report.latencyMs.avg},p95_ms=${report.latencyMs.p95},peak_ms=${report.latencyMs.max},wall_ms=${report.wallClockMs}`;
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
      console.log(`InfluxDB: wrote signalr_load to ${INFLUX_DB}`);
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

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
