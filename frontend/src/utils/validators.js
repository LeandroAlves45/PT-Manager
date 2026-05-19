/**
 * validators.js — regras de validação reutilizáveis para react-hook-form.
 *
 * Uso:
 *   import { emailRules, passwordRules } from '@/utils/validators';
 *   <Input {...register('email', emailRules)} />
 */

// ---------------------------------------------------------------------------
// Padrões regex
// ---------------------------------------------------------------------------

export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
export const HEX_COLOR_REGEX = /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/;
export const URL_REGEX = /^https?:\/\/.+/;

// ---------------------------------------------------------------------------
// Regras para react-hook-form
// ---------------------------------------------------------------------------

/** Email — usado em LoginPage, TrainerSignupPage, ClientFormDialog */
export const emailRules = {
  required: 'Email é obrigatório',
  pattern: {
    value: EMAIL_REGEX,
    message: 'Email inválido',
  },
};

/** Password cliente — mínimo 8 caracteres */
export const passwordRules = {
  required: 'Password é obrigatória',
  minLength: {
    value: 8,
    message: 'A password deve ter pelo menos 8 caracteres',
  },
};

/** Password Personal Trainer — mínimo 8 caracteres */
export const trainerPasswordRules = {
  required: 'A password é obrigatória',
  minLength: {
    value: 8,
    message: 'A password deve ter no mínimo 8 caracteres',
  },
};

/**
 * Regras de confirmação de password.
 * @param {string} passwordValue — valor atual do campo password
 */
export const confirmPasswordRules = (passwordValue) => ({
  required: 'Confirmação de password é obrigatória',
  validate: (value) => value === passwordValue || 'As passwords não coincidem',
});

/** Nome completo — mínimo 2 caracteres */
export const fullNameRules = {
  required: 'Nome é obrigatório',
  minLength: { value: 2, message: 'Mínimo 2 caracteres' },
};

/** Telemóvel — mínimo 9 dígitos (formato PT) */
export const phoneRules = {
  required: 'Telemóvel é obrigatório',
  minLength: { value: 9, message: 'Mínimo de 9 caracteres' },
};

/** Altura em cm (60–260) */
export const heightRules = {
  required: 'Altura é obrigatória',
  min: { value: 60, message: 'Mínimo de 60 cm' },
  max: { value: 260, message: 'Máximo de 260 cm' },
};

/** Cor hex #RRGGBB ou #RGB */
export const hexColorRules = {
  required: 'A cor é obrigatória',
  pattern: {
    value: HEX_COLOR_REGEX,
    message: 'Formato inválido. Use #RRGGBB ou #RGB',
  },
};

/** URL de vídeo (http/https) */
export const videoUrlRules = {
  pattern: {
    value: URL_REGEX,
    message: 'URL inválido — deve começar com http:// ou https://',
  },
};

// ---------------------------------------------------------------------------
// Utilitários de pesquisa
// ---------------------------------------------------------------------------

/**
 * Remove acentos e diacríticos de uma string para pesquisa insensível a acentos.
 *
 * Porquê é necessário:
 *   .toLowerCase() não remove acentos — "bi" não encontra "Bícep" porque
 *   "bícep".includes("bi") é false em JavaScript.
 *
 *   normalize("NFD") decompõe caracteres acentuados nos seus componentes
 *   (ex: "é" → "e" + combining accent).
 *   O regex /\p{M}/gu remove os combining marks, deixando apenas a letra base.
 *
 * Exemplos:
 *   normalize("Bícep")  → "Bicep"
 *   normalize("Isquiotibiais") → "Isquiotibiais" (sem acentos, já correcto)
 *   normalize("Trapézio") → "Trapezio"
 *
 * @param {string} str
 * @returns {string} String sem acentos em minúsculas
 */

function normalize(str) {
  return (str ?? '')
    .normalize('NFD') // Decompõe caracteres acentuados
    .replace(/\p{M}/gu, '') // Remove os combining marks
    .toLowerCase(); // Converte para minúsculas
}

/**
 * Verifica se a query coincide com pelo menos um dos campos fornecidos.
 * Insensível a maiúsculas/minúsculas E a acentos.
 *
 * Exemplos:
 *   matchesSearch('bi', 'Bícep Curl com Halteres', 'Bíceps')  → true
 *   matchesSearch('bicep', 'nome do exercício', 'Bíceps')      → true
 *   matchesSearch('trap', 'Encolhimento', 'Trapézios')         → true
 *
 * @param {string} query  - Texto introduzido pelo utilizador
 * @param {...string} fields - Campos a pesquisar (aceita null/undefined)
 * @returns {boolean}
 */

export function matchesSearch(query, ...fields) {
  const q = normalize(query);
  return fields.some((field) => normalize(field).includes(q));
}
