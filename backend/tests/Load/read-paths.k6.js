// Carga das leituras repetitivas do PT Manager (Sprint 7A, S7-04).
//
// O k6 faz de personal trainers e de clientes a abrir as páginas mais usadas. Os números
// que o Gate 7B usa vêm do lado do servidor, pelo OpenTelemetry (p95 de
// http.server.request.duration e ptm.http.db_commands por rota). O resumo do k6 é só a
// confirmação do lado do cliente.
//
// Variáveis:
//   BASE_URL        API, por omissão http://host.docker.internal:5045
//   ORIGIN          origem aceite pelo CORS, por omissão http://localhost:5173
//   SEED_PASSWORD   palavra-passe das contas do seed de desenvolvimento (obrigatória)
//   EXTRA_CLIENTS   clientes a criar no setup, por omissão 40
//
// Cada actor fica abaixo de 300 pedidos por minuto, o limite global por utilizador: o que
// se mede é a latência e não o 429. O cenário login_burst força o auth_login (10/min por
// IP) para a métrica de rejeições do rate limiting ter dados.

import http from 'k6/http';
import { check, fail, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5045';
const ORIGIN = __ENV.ORIGIN || 'http://localhost:5173';
const PASSWORD = __ENV.SEED_PASSWORD;
const EXTRA_CLIENTS = Number(__ENV.EXTRA_CLIENTS || 40);

export const options = {
  scenarios: {
    trainer_reads: {
      executor: 'constant-vus',
      exec: 'trainerReads',
      vus: 4,
      duration: '3m',
      startTime: '10s',
    },
    client_reads: {
      executor: 'constant-vus',
      exec: 'clientReads',
      vus: 4,
      duration: '3m',
      startTime: '10s',
    },
    login_burst: {
      executor: 'per-vu-iterations',
      exec: 'loginBurst',
      vus: 1,
      iterations: 15,
      startTime: '1m',
    },
  },
  thresholds: {
    // Confirmação do lado do cliente; o critério do gate é o do servidor.
    'http_req_duration{kind:read}': ['p(95)<300'],
    'checks{kind:read}': ['rate>0.99'],
  },
};

function jsonHeaders(token) {
  const headers = { 'Content-Type': 'application/json', Origin: ORIGIN };
  if (token)
    headers.Authorization = `Bearer ${token}`;
  return headers;
}

function login(email) {
  const response = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password: PASSWORD }),
    { headers: jsonHeaders(), tags: { kind: "setup" } },
  );
  if (response.status !== 200)
    fail(`login ${email} -> ${response.status}`);
  return response.json("access_token");
}

export function setup() {
  if (!PASSWORD)
    fail("SEED_PASSWORD is required");

  const trainerToken = login("trainer@ptmanager.local");
  const clientToken = login("cliente@ptmanager.local");

  for (let index = 0; index < EXTRA_CLIENTS; index += 1) {
    const response = http.post(
      `${BASE_URL}/api/v1/clients`,
      JSON.stringify({
        name: `Cliente Carga ${index}`,
        contact_email: null,
        // O telefone é único por personal trainer.
        phone: `+35193${String(index).padStart(7, "0")}`,
        birth_date: "1990-05-17",
        sex: index % 2 === 0 ? "female" : "male",
        objective: null,
        notes: null,
        emergency_contact_name: null,
        emergency_contact_phone: null,
      }),
      { headers: jsonHeaders(trainerToken), tags: { kind: "setup" } },
    );
    if (response.status !== 201)
      fail(`create client ${index} -> ${response.status} ${response.body}`);
  }

  const clients = http.get(`${BASE_URL}/api/v1/clients?page_size=50`, {
    headers: jsonHeaders(trainerToken),
    tags: { kind: "setup" },
  });
  const clientIds = clients.json("items").map((item) => item.id);

  return { trainerToken, clientToken, clientIds };
}

function read(token, path, name) {
  const response = http.get(`${BASE_URL}${path}`, {
    headers: jsonHeaders(token),
    tags: { kind: "read", name },
  });
  check(
    response,
    { [`${name} 200`]: (r) => r.status === 200 },
    { kind: "read" },
  );
}

export function trainerReads(data) {
  const clientId =
    data.clientIds[Math.floor(Math.random() * data.clientIds.length)];
  read(data.trainerToken, "/api/v1/dashboard", "dashboard");
  read(data.trainerToken, "/api/v1/clients?page_size=20", "clients");
  read(data.trainerToken, `/api/v1/clients/${clientId}`, "client");
  read(
    data.trainerToken,
    `/api/v1/clients/${clientId}/summary`,
    "client_summary",
  );
  read(data.trainerToken, "/api/v1/sessions", "sessions");
  read(data.trainerToken, "/api/v1/exercises", "exercises");
  read(data.trainerToken, "/api/v1/foods", "foods");
  read(data.trainerToken, "/api/v1/training-plans", "training_plans");
  // 8 pedidos em ~8 s por VU: 4 VUs ≈ 240 pedidos/min para o mesmo utilizador.
  sleep(8);
}

export function clientReads(data) {
  read(data.clientToken, "/api/v1/portal/home", "portal_home");
  read(
    data.clientToken,
    "/api/v1/portal/my-workout/today",
    "portal_workout_today",
  );
  read(data.clientToken, "/api/v1/portal/my-plan", "portal_plan");
  // O my-nutrition dá 404 até o cliente escolher um plano (U68); a lista é a leitura de antes.
  read(data.clientToken, "/api/v1/portal/my-meal-plans", "portal_meal_plans");
  read(data.clientToken, "/api/v1/portal/my-supplements", "portal_supplements");
  read(
    data.clientToken,
    "/api/v1/portal/my-check-ins/next",
    "portal_next_check_in",
  );
  sleep(6);
}

export function loginBurst() {
  http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({
      email: "nobody@ptmanager.local",
      password: "wrong-password",
    }),
    { headers: jsonHeaders(), tags: { kind: "burst" } },
  );
}
