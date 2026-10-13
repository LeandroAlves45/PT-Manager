import { z } from 'zod';

import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';

/**
 * Regras do perfil do cliente: contactos e fotografia.
 *
 * Os limites são os de `UpdateMyProfileCommandValidator` e do perfil de imagem `ClientAvatar`
 * (`ImageProfiles.cs`).
 */

type Profile = components['schemas']['MyProfileResponse'];
type ProfileUpdate = components['schemas']['UpdateMyProfileRequest'];

/**
 * Mesma regra do value object `EmailAddress` do backend: sem espaços, um só `@`, texto dos
 * dois lados e um domínio com ponto que não começa nem acaba em ponto.
 */
export function isAcceptedEmail(email: string): boolean {
  if (/\s/.test(email)) return false;

  const parts = email.split('@');
  if (parts.length !== 2) return false;

  const [local, domain] = parts;
  if (local === undefined || domain === undefined || local === '' || domain === '') return false;

  return domain.includes('.') && !domain.startsWith('.') && !domain.endsWith('.');
}

export const profileSchema = z.object({
  contact_email: z
    .string()
    .trim()
    .max(255, 'O email não pode exceder 255 caracteres.')
    .refine((value) => value === '' || isAcceptedEmail(value), 'Indica um email válido.'),
  phone: z
    .string()
    .trim()
    .min(1, 'Indica o telefone.')
    .max(32, 'O telefone não pode exceder 32 caracteres.'),
  emergency_contact_name: z.string().trim().max(255, 'O nome não pode exceder 255 caracteres.'),
  emergency_contact_phone: z.string().trim().max(32, 'O telefone não pode exceder 32 caracteres.'),
});

export type ProfileValues = z.input<typeof profileSchema>;

/** Valores do formulário a partir do perfil; um opcional sem valor fica vazio. */
export function toProfileValues(profile: Profile): ProfileValues {
  return {
    contact_email: profile.contact_email ?? '',
    phone: profile.phone ?? '',
    emergency_contact_name: profile.emergency_contact_name ?? '',
    emergency_contact_phone: profile.emergency_contact_phone ?? '',
  };
}

const orNull = (value: string) => (value === '' ? null : value);

/**
 * Pedido do `PATCH`, que substitui os quatro campos: um opcional vazio vai a `null` e apaga
 * o valor guardado.
 */
export function toProfileUpdate(values: z.output<typeof profileSchema>): ProfileUpdate {
  return {
    contact_email: orNull(values.contact_email),
    phone: values.phone,
    emergency_contact_name: orNull(values.emergency_contact_name),
    emergency_contact_phone: orNull(values.emergency_contact_phone),
  };
}

/** Rótulos do sexo biológico (`BiologicalSex`), só de leitura no perfil. */
export const PROFILE_SEX_LABELS: Readonly<Record<string, string>> = {
  male: 'Masculino',
  female: 'Feminino',
};

/** Iniciais para o avatar sem fotografia: primeira letra do primeiro e do último nome. */
export function profileInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return `${first}${last}`.toUpperCase();
}

const PROFILE_MESSAGES: Readonly<Record<string, string>> = {
  client_email_invalid: 'Indica um email válido.',
  client_email_too_long: 'O email não pode exceder 255 caracteres.',
  client_email_already_exists: 'Este email já está a ser usado noutra ficha de cliente.',
  client_phone_invalid: 'Indica o telefone.',
  client_phone_too_long: 'O telefone não pode exceder 32 caracteres.',
  client_phone_already_exists: 'Este telefone já está a ser usado noutra ficha de cliente.',
  client_emergency_contact_name_too_long: 'O nome não pode exceder 255 caracteres.',
  client_emergency_contact_phone_too_long: 'O telefone não pode exceder 32 caracteres.',
  portal_profile_not_available: 'A tua ficha não está ativa. Fala com o teu personal trainer.',
};

/**
 * Campo do formulário a que cada código pertence. Os dois 409 de unicidade chegam no `title`,
 * sem `errors[]`, e mesmo assim têm um campo óbvio.
 */
const PROFILE_CODE_FIELDS: Readonly<Record<string, keyof ProfileValues>> = {
  client_email_invalid: 'contact_email',
  client_email_too_long: 'contact_email',
  client_email_already_exists: 'contact_email',
  client_phone_invalid: 'phone',
  client_phone_too_long: 'phone',
  client_phone_already_exists: 'phone',
  client_emergency_contact_name_too_long: 'emergency_contact_name',
  client_emergency_contact_phone_too_long: 'emergency_contact_phone',
};

/** Falha ao guardar os contactos: a mensagem e o campo onde a mostrar (`root` sem campo). */
export function profileFailure(failure: unknown): {
  field: keyof ProfileValues | 'root';
  message: string;
} {
  const fallback = 'Não foi possível guardar os contactos. Tenta novamente.';
  if (!isApiProblem(failure)) return { field: 'root', message: fallback };

  // 400: o código em `errors[]`; 404 e 409: está em `title`.
  const code = failure.fieldErrors[0]?.code ?? failure.code;
  if (!Object.hasOwn(PROFILE_MESSAGES, code)) return { field: 'root', message: fallback };

  return {
    field: PROFILE_CODE_FIELDS[code] ?? 'root',
    message: PROFILE_MESSAGES[code] ?? fallback,
  };
}

/** Formatos aceites pelo perfil de imagem `ClientAvatar`. */
export const AVATAR_CONTENT_TYPES = ['image/png', 'image/jpeg', 'image/webp'] as const;

/** 4 MiB, o limite do perfil de imagem `ClientAvatar`. */
export const AVATAR_MAX_BYTES = 4 * 1024 * 1024;

/**
 * Valida a fotografia antes do pedido: tipo e tamanho. As dimensões (64–4096 px) e o
 * conteúdo só o servidor consegue verificar.
 */
export function avatarFileError(file: File): string | null {
  if (!(AVATAR_CONTENT_TYPES as readonly string[]).includes(file.type))
    return 'Escolhe uma imagem PNG, JPEG ou WebP.';

  if (file.size === 0) return 'O ficheiro está vazio.';
  if (file.size > AVATAR_MAX_BYTES) return 'A fotografia não pode exceder 4 MB.';

  return null;
}

const INVALID_IMAGE = 'Escolhe uma imagem PNG, JPEG ou WebP válida.';
const TOO_LARGE = 'A fotografia não pode exceder 4 MB.';
const TOO_MANY_PIXELS = 'A imagem é demasiado grande. Usa uma com menos de 4096 px de lado.';
const UNAVAILABLE =
  'Não é possível enviar fotografias neste momento. Nada foi alterado. Tenta mais tarde.';

/**
 * Códigos de `MediaPreparationErrorMapper` (prefixo `portal_avatar`) e do validador do upload.
 * A recusa da moderação não diz porquê, de propósito: o backend não distingue "recusada" de
 * "a rever".
 */
const AVATAR_MESSAGES: Readonly<Record<string, string>> = {
  portal_avatar_required: 'O ficheiro está vazio.',
  portal_avatar_empty: 'O ficheiro está vazio.',
  portal_avatar_too_large: TOO_LARGE,
  portal_avatar_media_too_large: TOO_LARGE,
  portal_avatar_unsupported_media_type: INVALID_IMAGE,
  portal_avatar_unsupported_format: INVALID_IMAGE,
  portal_avatar_content_type_mismatch: INVALID_IMAGE,
  portal_avatar_not_decodable: INVALID_IMAGE,
  portal_avatar_dimensions_too_small: 'A imagem tem de ter pelo menos 64 px de lado.',
  portal_avatar_dimensions_too_large: TOO_MANY_PIXELS,
  portal_avatar_pixel_budget_exceeded: TOO_MANY_PIXELS,
  portal_avatar_encoding_failed: 'Não foi possível processar a imagem. Experimenta outra.',
  portal_avatar_content_not_allowed: 'Esta imagem não foi aceite. Escolhe outra fotografia.',
  portal_avatar_moderation_unavailable: UNAVAILABLE,
  portal_avatar_storage_unavailable: UNAVAILABLE,
  portal_profile_not_available: 'A tua ficha não está ativa. Fala com o teu personal trainer.',
};

/**
 * Mensagem PT para a falha de enviar ou remover a fotografia.
 *
 * O limite de envios (10 por hora) reconhece-se pelo estado 429: o corpo desse erro não traz
 * um código estável, só o título "Too many requests".
 *
 * @param fallback Mensagem quando o código não é conhecido (depende da ação).
 */
export function avatarFailureMessage(failure: unknown, fallback: string): string {
  if (!isApiProblem(failure)) return fallback;
  if (failure.status === 429)
    return 'Enviaste demasiadas fotografias. Tenta de novo dentro de 1 hora.';

  // 400: o código em `errors[]`; 404 e 503: está no `title`.
  const code = failure.fieldErrors[0]?.code ?? failure.code;
  return (Object.hasOwn(AVATAR_MESSAGES, code) ? AVATAR_MESSAGES[code] : undefined) ?? fallback;
}
