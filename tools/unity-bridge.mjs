#!/usr/bin/env node
/**
 * Unity MCP WebSocket Bridge
 * Sends commands directly to Unity Editor's WebSocket server.
 * Usage: node unity-bridge.mjs <method> [params_json]
 */

import { randomUUID } from 'crypto';

const UNITY_WS_URL = 'ws://localhost:8090/McpUnity';
const TIMEOUT_MS = 120000;
const CONNECT_DELAY_MS = 300;

const method = process.argv[2];
const params = process.argv[3] ? JSON.parse(process.argv[3]) : {};

if (!method) {
  console.error('Usage: node unity-bridge.mjs <method> [params_json]');
  process.exit(1);
}

const requestId = randomUUID();
const request = { id: requestId, method, params };

const ws = new WebSocket(UNITY_WS_URL);

const timeout = setTimeout(() => {
  console.error(`Request timed out after ${TIMEOUT_MS}ms`);
  ws.close();
  process.exit(2);
}, TIMEOUT_MS);

ws.addEventListener('open', () => {
  setTimeout(() => ws.send(JSON.stringify(request)), CONNECT_DELAY_MS);
});

ws.addEventListener('message', (event) => {
  clearTimeout(timeout);
  try {
    const response = JSON.parse(event.data);
    if (response.error) {
      console.error('Unity Error:', JSON.stringify(response.error, null, 2));
      ws.close();
      process.exit(3);
    } else {
      console.log(JSON.stringify(response.result || response, null, 2));
      ws.close();
      process.exit(0);
    }
  } catch (e) {
    console.error('Parse error:', e.message);
    console.log('Raw:', event.data);
    ws.close();
    process.exit(4);
  }
});

ws.addEventListener('error', (event) => {
  clearTimeout(timeout);
  console.error('WebSocket error:', event.message || 'Connection failed');
  process.exit(5);
});

ws.addEventListener('close', () => {
  clearTimeout(timeout);
});
