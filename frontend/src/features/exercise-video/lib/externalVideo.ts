/**
 * Ligação de vídeo externa de um exercício (YouTube ou Vimeo).
 *
 * Espelha `ExternalVideoUrlRules` do backend, que é quem decide. Aqui serve para avisar no
 * formulário antes de gravar e, no portal, para nunca transformar em link um endereço que não
 * seja HTTPS de um destes sites (defesa em profundidade se um valor antigo escapar à regra).
 */

export type ExternalVideoProvider = 'YouTube' | 'Vimeo';

/** Tamanho da coluna `exercises.video_url`. */
export const EXTERNAL_VIDEO_URL_MAX_LENGTH = 500;

export const EXTERNAL_VIDEO_URL_ERROR =
  'Usa uma ligação do YouTube ou do Vimeo que comece por https://.';

const PROVIDERS: ReadonlyMap<string, ExternalVideoProvider> = new Map([
  ['youtube.com', 'YouTube'],
  ['www.youtube.com', 'YouTube'],
  ['m.youtube.com', 'YouTube'],
  ['youtu.be', 'YouTube'],
  ['vimeo.com', 'Vimeo'],
  ['www.vimeo.com', 'Vimeo'],
]);

/** Ligação pronta a abrir: o endereço normalizado e o nome do site para o botão. */
export interface ExternalVideoLink {
  readonly href: string;
  readonly provider: ExternalVideoProvider;
}

/**
 * Valida e normaliza a ligação: HTTPS, sem utilizador nem porta e com o host exatamente na
 * lista (por isso `youtube.com.exemplo.pt` não passa). Devolve `null` para vazio ou inválido.
 */
export function externalVideoLink(value: string | null | undefined): ExternalVideoLink | null {
  const trimmed = value?.trim() ?? '';
  if (trimmed === '' || trimmed.length > EXTERNAL_VIDEO_URL_MAX_LENGTH) return null;
  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return null;
  }
  if (url.protocol !== 'https:' || url.username !== '' || url.password !== '' || url.port !== '')
    return null;
  const provider = PROVIDERS.get(url.hostname);
  return provider === undefined ? null : { href: url.href, provider };
}

/** Campo do formulário: vazio (sem ligação) ou uma ligação aceite. */
export function isValidExternalVideoUrl(value: string): boolean {
  return value.trim() === '' || externalVideoLink(value) !== null;
}
