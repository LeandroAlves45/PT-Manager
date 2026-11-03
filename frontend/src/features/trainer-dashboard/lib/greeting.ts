/**
 * Saudação do painel do personal trainer.
 *
 * Na primeira visita (neste dispositivo) dá as boas-vindas; depois roda entre frases de
 * incentivo, uma por dia, para não mudar a cada clique. Funções puras, exceto a memória da
 * primeira visita, que vive no `localStorage` e nunca parte o ecrã se o browser a recusar.
 */

/** Saudação a mostrar e o emoji (decorativo, escondido dos leitores de ecrã). */
export interface DashboardGreeting {
  readonly text: string;
  readonly emoji: string | null;
}

/** Primeiro nome da conta; `null` sem nome (contas antigas). */
export function firstName(fullName: string | null | undefined): string | null {
  const first = fullName?.trim().split(/\s+/)[0] ?? '';
  return first === '' ? null : first;
}

const DAILY: readonly ((name: string | null) => DashboardGreeting)[] = [
  (name) => ({
    text:
      name === null
        ? 'Vamos acompanhar os teus clientes'
        : `Vamos acompanhar os teus clientes, ${name}`,
    emoji: null,
  }),
  (name) => ({
    text:
      name === null
        ? 'Vamos ajudar os teus clientes a chegar aos resultados?'
        : `Vamos ajudar os teus clientes a chegar aos resultados, ${name}?`,
    emoji: '💪',
  }),
  (name) => ({
    text: name === null ? 'Bom treino' : `Bom treino, ${name}`,
    emoji: null,
  }),
];

/**
 * @param fullName Nome da conta do personal trainer (`trainer_name`).
 * @param firstVisit Primeira vez que o painel abre neste dispositivo.
 * @param localToday Dia local do personal trainer (`YYYY-MM-DD`), escolhe a frase do dia.
 */
export function dashboardGreeting(
  fullName: string | null | undefined,
  firstVisit: boolean,
  localToday: string | undefined
): DashboardGreeting {
  const name = firstName(fullName);
  if (firstVisit)
    return { text: name === null ? 'Boas-vindas' : `Boas-vindas, ${name}`, emoji: null };
  const day =
    localToday === undefined ? 0 : Math.floor(Date.parse(`${localToday}T00:00:00Z`) / 86_400_000);
  const pick = DAILY[((day % DAILY.length) + DAILY.length) % DAILY.length]!;
  return pick(name);
}

const WELCOMED_PREFIX = 'ptm.dashboard.welcomed.';

/**
 * Primeira visita deste utilizador neste dispositivo? Só lê: a marca grava-se à parte
 * (`markWelcomed`), num efeito, porque o StrictMode corre o inicializador do estado duas vezes.
 * Sem `localStorage` (modo privado, bloqueado) conta como já visto, para não repetir as
 * boas-vindas em cada visita.
 */
export function isFirstVisit(userId: string | null | undefined): boolean {
  if (!userId) return false;
  try {
    // eslint-disable-next-line no-restricted-globals -- marca de boas-vindas, não é dado de sessão
    return localStorage.getItem(WELCOMED_PREFIX + userId) === null;
  } catch {
    return false;
  }
}

/** Regista que o utilizador já viu as boas-vindas neste dispositivo. */
export function markWelcomed(userId: string | null | undefined): void {
  if (!userId) return;
  try {
    // eslint-disable-next-line no-restricted-globals -- marca de boas-vindas, não é dado de sessão
    localStorage.setItem(WELCOMED_PREFIX + userId, '1');
  } catch {
    // Sem armazenamento: as boas-vindas não se repetem por outra via, e o ecrã não parte.
  }
}
