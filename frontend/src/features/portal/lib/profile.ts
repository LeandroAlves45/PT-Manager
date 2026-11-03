import { z } from 'zod';

import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';

/**
 * Regras do perfil do cliente: nome, contactos e fotografia.
 *
 * Os limites são os de `UpdateMyProfileCommandValidator` e do perfil de imagem `ClientAvatar`
 * (`ImageProfiles.cs`).
 */

type Profile = components['schemas']['MyProfileResponse'];
type ProfileUpdate = components['schemas']['UpdateMyProfileRequest'];

export const profileSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Indica o teu nome.')
    .max(255, 'O nome não pode exceder 255 caracteres.')
    // O backend recusa U+0000 (`text_invalid_character`); aqui recusam-se também os outros
    // caracteres de controlo, que nunca fazem parte de um nome. Escritos em escape para o
    // ficheiro continuar a ser texto.
    // eslint-disable-next-line no-control-regex
    .refine((value) => !/[\u0000-\u001f\u007f]/.test(value), 'O nome tem caracteres inválidos.'),
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
    name: profile.name,
    phone: profile.phone,
    emergency_contact_name: profile.emergency_contact_name ?? '',
    emergency_contact_phone: profile.emergency_contact_phone ?? '',
  };
}

const orNull = (value: string) => (value === '' ? null : value);

/**
 * Pedido do `PATCH`, que substitui os quatro campos: um opcional vazio vai a `null` e apaga
 * o valor guardado. O email da ficha não faz parte do pedido.
 */
export function toProfileUpdate(values: z.output<typeof profileSchema>): ProfileUpdate {
  return {
    name: values.name,
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

const INVALID_CHARACTER = 'O texto tem caracteres inválidos.';

const PROFILE_MESSAGES: Readonly<Record<string, string>> = {
  client_name_invalid: 'Indica o teu nome, com até 255 caracteres.',
  client_phone_invalid: 'Indica o telefone.',
  client_phone_too_long: 'O telefone não pode exceder 32 caracteres.',
  client_phone_already_exists: 'Este telefone já está a ser usado noutra ficha de cliente.',
  client_emergency_contact_name_too_long: 'O nome não pode exceder 255 caracteres.',
  client_emergency_contact_phone_too_long: 'O telefone não pode exceder 32 caracteres.',
  text_invalid_character: INVALID_CHARACTER,
  portal_profile_not_available: 'A tua ficha não está ativa. Fala com o teu personal trainer.',
};

/**
 * Campo do formulário a que cada código pertence. O 409 de unicidade do telefone chega no
 * `title`, sem `errors[]`, e mesmo assim tem um campo óbvio.
 */
const PROFILE_CODE_FIELDS: Readonly<Record<string, keyof ProfileValues>> = {
  client_name_invalid: 'name',
  client_phone_invalid: 'phone',
  client_phone_too_long: 'phone',
  client_phone_already_exists: 'phone',
  client_emergency_contact_name_too_long: 'emergency_contact_name',
  client_emergency_contact_phone_too_long: 'emergency_contact_phone',
};

/**
 * Campo do formulário pelo nome que o servidor dá ao campo do pedido (`Name`,
 * `EmergencyContactPhone`…). Serve os códigos genéricos, como `text_invalid_character`, que
 * não dizem a que campo pertencem.
 */
const PROFILE_REQUEST_FIELDS: Readonly<Record<string, keyof ProfileValues>> = {
  name: 'name',
  phone: 'phone',
  emergencycontactname: 'emergency_contact_name',
  emergencycontactphone: 'emergency_contact_phone',
};

/** Falha ao guardar os dados: a mensagem e o campo onde a mostrar (`root` sem campo). */
export function profileFailure(failure: unknown): {
  field: keyof ProfileValues | 'root';
  message: string;
} {
  const fallback = 'Não foi possível guardar os dados. Tenta novamente.';
  if (!isApiProblem(failure)) return { field: 'root', message: fallback };

  // 400: o código em `errors[]`; 404 e 409: está em `title`.
  const first = failure.fieldErrors[0];
  const code = first?.code ?? failure.code;
  if (!Object.hasOwn(PROFILE_MESSAGES, code)) return { field: 'root', message: fallback };

  const requestField =
    first === undefined ? undefined : PROFILE_REQUEST_FIELDS[normalized(first.field)];

  return {
    field: PROFILE_CODE_FIELDS[code] ?? requestField ?? 'root',
    message: PROFILE_MESSAGES[code] ?? fallback,
  };
}

/** `EmergencyContactName`, `emergency_contact_name` e `emergencyContactName` dão a mesma chave. */
function normalized(field: string): string {
  return field.replaceAll('_', '').toLowerCase();
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
