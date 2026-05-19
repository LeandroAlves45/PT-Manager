/**
 * formatters.js — utilitários de formatação usados em toda a aplicação.
 */

/**
 * Formata uma data para o formato pt-PT (DD/MM/AAAA).
 * Usado em: MyCheckins, AlertsPanel, etc.
 * @param {string|Date} date
 * @returns {string}
 */
export function formatDate(date) {
  if (!date) return '—';
  return new Date(date).toLocaleDateString('pt-PT');
}

/**
 * Formata uma data+hora para o formato pt-PT com hora e minuto.
 * Usado em: AlertsPanel
 * @param {string|Date} date
 * @returns {string}
 */
export function formatDateTime(date) {
  return new Date(date).toLocaleString('pt-PT', {
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Diferença em horas entre duas datas.
 * @param {Date} from
 * @param {Date} to
 * @returns {number}
 */
export function diffInHours(from, to) {
  return (to - from) / (1000 * 60 * 60);
}

/**
 * Converte uma cor hex (#RRGGBB) para o formato HSL usado nas CSS variables do tema.
 * Usado em: AuthContext
 * @param {string} hex
 * @returns {string} ex: "220 90% 56%"
 */
export function hexToHSL(hex) {
  const clean = hex.replace('#', '');
  const r = parseInt(clean.substring(0, 2), 16) / 255;
  const g = parseInt(clean.substring(2, 4), 16) / 255;
  const b = parseInt(clean.substring(4, 6), 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const delta = max - min;

  let h = 0;
  let s = 0;
  const l = (max + min) / 2;

  if (delta !== 0) {
    s = delta / (1 - Math.abs(2 * l - 1));
    switch (max) {
      case r: h = ((g - b) / delta + (g < b ? 6 : 0)) / 6; break;
      case g: h = ((b - r) / delta + 2) / 6; break;
      case b: h = ((r - g) / delta + 4) / 6; break;
    }
  }

  return `${Math.round(h * 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}

/**
 * Faz parse de um valor de formulário para float, devolvendo null se vazio.
 * @param {string} value
 * @returns {number|null}
 */
export function parseNullableFloat(value) {
  return value !== '' && value != null ? parseFloat(value) : null;
}

/**
 * Faz parse de um valor de formulário para inteiro, devolvendo null se vazio.
 * @param {string} value
 * @returns {number|null}
 */
export function parseNullableInt(value) {
  return value !== '' && value != null ? parseInt(value, 10) : null;
}
