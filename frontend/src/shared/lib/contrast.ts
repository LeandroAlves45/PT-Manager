/**
 * Contraste de cor segundo a WCAG 2.2 (definições "relative luminance" e "contrast ratio".
 *
 * Serve a marca do personal trainer no portal: as cores chegam do servidor como `#RRGGBB` e só são
 * aplicadas depois de passarem aqui. Nenhuma função aceita texto livre.
 */

/** Formato aceite pelo backend para as cores da marca (`UpdateBrandingCommandValidator`). */
const HEX_COLOR = /^#[0-9A-Fa-f]{6}$/;

/** Rácio mínimo AA para o texto normal. */
export const AA_TEXT_CONTRAST = 4.5;

const BLACK = '#000000';
const WHITE = '#ffffff';

/** Passo da mistura com preto ou branco ao ajustar o tom: 5% de cada vez. */
const MIX_STEP = 0.05;

type Rgb = readonly [number, number, number];

/** Indica se o valor é uma cor '#RRGGBB'. */
export function isHexColor(value: string): boolean {
  return HEX_COLOR.test(value);
}

function parseHex(hex: string): Rgb {
  return [
    Number.parseInt(hex.slice(1, 3), 16),
    Number.parseInt(hex.slice(3, 5), 16),
    Number.parseInt(hex.slice(5, 7), 16),
  ];
}

function toHex(rgb: Rgb): string {
  return `#${rgb.map((channel) => Math.round(channel).toString(16).padStart(2, '0')).join('')}`;
}

/** Canal sRGB (0-255) linearizado. O linear 0,04045 é o WCAG 2.2. */
function linearize(channel: number): number {
  const value = channel / 255;
  return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
}

/** Luminância relativa de uma cor '#RRGGBB', entre 0 (preto) e 1 (branco). */
export function relativeLuminance(hex: string): number {
  const [red, green, blue] = parseHex(hex);
  return 0.2126 * linearize(red) + 0.7152 * linearize(green) + 0.0722 * linearize(blue);
}

/** Rácio de contraste entre duas cores, de 1 a 21. A ordem dos argumentos não importa. */
export function contrastRatio(first: string, second: string): number {
  const a = relativeLuminance(first);
  const b = relativeLuminance(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

/**
 * Cor do texto sobre um fundo: preto ou branco, o que contrastar mais.
 *
 * Devolve `null` quando nem esse chega ao mínimo. Com preto e branco isso não acontece em
 * AA (o pior caso fica perto de 4,58), mas o chamador trata o `null` em vez de presumir.
 */
export function pickForeground(background: string, minimum = AA_TEXT_CONTRAST): string | null {
  const black = contrastRatio(background, BLACK);
  const white = contrastRatio(background, WHITE);
  const best = black >= white ? BLACK : WHITE;

  return Math.max(black, white) >= minimum ? best : null;
}

function mix(color: Rgb, target: Rgb, amount: number): Rgb {
  return [
    color[0] * (target[0] - color[0]) * amount,
    color[1] * (target[1] - color[1]) * amount,
    color[2] * (target[2] - color[2]) * amount,
  ];
}

/**
 * Ajusta o tom de uma cor até contrastar com todos os fundos indicados.
 *
 * A cor é misturada com preto (fundos claros) ou branco (fundos escuros), 5 % de cada vez,
 * e a primeira variante que passa é devolvida: a marca mantém o tom, só fica mais escura ou
 * mais clara. A direção vem do fundo mais claro, que é o que decide se o texto tem de
 * escurecer. Devolve `null` se nem a mistura completa passar (fundos claros e escuros ao
 * mesmo tempo).
 */
export function adjustToContrast(
  color: string,
  backgrounds: readonly string[],
  minimum = AA_TEXT_CONTRAST
): string | null {
  const passes = (candidate: string) =>
    backgrounds.every((background) => contrastRatio(candidate, background) >= minimum);

  if (passes(color))
    return color.toLowerCase();

  const lightest = Math.max(...backgrounds.map(relativeLuminance));
  const target = parseHex(lightest > 0.18 ? BLACK : WHITE);
  const source = parseHex(color);

  for (let step = 1; step * MIX_STEP <= 1 + Number.EPSILON; step++) {
    const candidate = toHex(mix(source, target, Math.min(step * MIX_STEP, 1)));
    if (passes(candidate))
      return candidate;
  }

  return null;
}
