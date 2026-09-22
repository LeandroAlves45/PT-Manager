// @ts-check
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { argv, env, exit } from 'node:process';

/**
 * Falha quando `schema.d.ts` deixou de corresponder ao OpenAPI do backend.
 *
 * É este guarda que transforma "os tipos vêm do contrato" numa garantia e não numa boa
 * intenção: sem ele, um campo renomeado no backend só apareceria em runtime, num ecrã,
 * já em produção de desenvolvimento.
 *
 * Uso: `node scripts/check-api-types.mjs [url]`. Sem argumento usa `OPENAPI_URL` ou o
 * endereço local por omissão.
 */

const OUTPUT = 'src/shared/api/schema.d.ts';
const url = argv[2] ?? env.OPENAPI_URL ?? 'http://localhost:5045/openapi/v1.json';

const current = readFileSync(OUTPUT, 'utf-8');

const generated = execFileSync(
  process.platform === 'win32' ? 'npx.cmd' : 'npx',
  ['openapi-typescript', url],
  { encoding: 'utf-8', maxBuffer: 64 * 1024 * 1024 }
);

// Normaliza só os fins de linha: tudo o resto é diferente a sério.
/** @param {string} value */
const normalise = (value) => value.replace(/\r\n/g, '\n').trimEnd();

if (normalise(current) !== normalise(generated)) {
  console.error(
    `${OUTPUT} está desatualizado do contrato em ${url}.\n` +
    'Corre `npm run api:types` com o backend a correr em Development e revê o diff.'
  );
  exit(1);
}

console.log(`${OUTPUT} corresponde ao contrato em ${url}.`);
