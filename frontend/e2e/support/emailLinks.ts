import { readFile } from 'node:fs/promises';

/** Tipos de email que o backend escreve no log em Development (`LoggingAuthenticationEmailSender`). */
export type DevelopmentEmailType = 'email_confirmation' | 'client_invitation';

const POLL_INTERVAL_MS = 500;

/**
 * Lê do log da API o link do email enviado a `recipient`.
 *
 * Em Development o backend não envia emails: escreve `Development email {tipo} sent to
 * {destinatário}: {link}`. Devolve o link mais recente, à espera até `timeoutMs` que a linha
 * apareça.
 */
export async function readEmailLink(
  type: DevelopmentEmailType,
  recipient: string,
  timeoutMs = 20_000
): Promise<string> {
  const logPath = process.env.E2E_API_LOG;
  if (!logPath) throw new Error('Define E2E_API_LOG com o ficheiro de log da API.');

  const pattern = new RegExp(
    `Development email ${type} sent to ${escapeRegExp(recipient)}: ([^\\s"\\\\]+)`,
    'g'
  );
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const log = await readFile(logPath, 'utf8').catch(() => '');
    const last = [...log.matchAll(pattern)].at(-1)?.[1];
    if (last !== undefined) return last;
    await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
  }
  throw new Error(`Link ${type} para ${recipient} não apareceu em ${logPath}.`);
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
