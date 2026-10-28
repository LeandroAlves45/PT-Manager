/**
 * Formata uma data `AAAA-MM-DD` por extenso em português ("28 de outubro de 2026").
 *
 * Calcula em UTC: o resultado não depende do fuso horário da máquina que faz o build, e o
 * HTML pré-renderizado bate certo com a hidratação no browser.
 *
 * @throws Error se o texto não for uma data `AAAA-MM-DD`.
 */
export function formatLongDate(isoDate: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoDate);
  if (!match) throw new Error(`Data inválida (esperado AAAA-MM-DD): "${isoDate}"`);
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
  return new Intl.DateTimeFormat('pt-PT', { dateStyle: 'long', timeZone: 'UTC' }).format(date);
}
