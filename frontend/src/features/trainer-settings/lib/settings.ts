import { isApiProblem } from '@/shared/api/problem';

/**
 * Regras e mensagens das definições do personal trainer — fonte única.
 *
 * Limites de `UpdateBrandingCommandValidator`, `UpdateContactsCommandValidator`,
 * `ChangeTimezoneCommandValidator` e `ImageProfiles.TrainerLogo`.
 */

/** Cor hexadecimal aceite pelo servidor (`^#[0-9A-Fa-f]{6}$`). */
export const HEX_COLOR = /^#[0-9A-Fa-f]{6}$/;

/** Formato de logo aceites na fronteira, antes de qualquer descodificação. */
export const LOGO_CONTENT_TYPES = ['image/png', 'image/jpeg', 'image/webp'] as const;

/** Tamanho máximo do logo: 5 MB. */
export const LOGO_MAX_BYTES = 5 * 1024 * 1024;

/**
 * Valida o ficheiro escolhido com o que se sabe sem o abrir. Dimensões (64–4096 px) e
 * conteúdo real só o servidor verifica; a recusa dele chega como código estável.
 *
 * @returns A mensagem de erro, ou `null` se o ficheiro pode seguir.
 */
export function logoFileError(file: File): string | null {
  if (!(LOGO_CONTENT_TYPES as readonly string[]).includes(file.type))
    return 'Escolhe uma imagem PNG, JPEG ou WebP.';

  if (file.size === 0) return 'O ficheiro está vazio.';
  if (file.size > LOGO_MAX_BYTES) return 'O logo não pode exceder 5 MB.';

  return null;
}

/** Mensagens dos códigos estáveis de `TrainerSettingsErrors` e `MediaPreparationErrorMapper`. */
const SETTINGS_ERRORS: Readonly<Record<string, string>> = {
  trainer_settings_app_name_required: 'Indica o nome da app.',
  trainer_settings_app_name_length: 'O nome da app tem de ter entre 2 e 50 caracteres.',
  trainer_settings_primary_color_invalid: 'Usa uma cor no formato #RRGGBB.',
  trainer_settings_body_color_invalid: 'Usa uma cor no formato #RRGGBB.',
  trainer_settings_phone_too_long: 'O telefone não pode exceder 20 caracteres.',
  trainer_settings_address_too_long: 'A morada não pode exceder 500 caracteres.',
  trainer_settings_city_too_long: 'A cidade não pode exceder 255 caracteres.',
  trainer_settings_timezone_required: 'Escolhe o fuso horário.',
  trainer_settings_invalid_timezone: 'Este fuso horário não é reconhecido.',
  trainer_settings_schedule_conflict:
    'Com este fuso, um cliente ficaria com duas sessões agendadas no mesmo dia. Reagenda uma delas primeiro.',
  trainer_settings_logo_required: 'Escolhe uma imagem.',
  trainer_settings_unsupported_media_type: 'Escolhe uma imagem PNG, JPEG ou WebP.',
  trainer_settings_media_too_large: 'O logo não pode exceder 5 MB.',
  trainer_settings_logo_empty: 'O ficheiro está vazio.',
  trainer_settings_logo_too_large: 'O logo não pode exceder 5 MB.',
  trainer_settings_logo_unsupported_format: 'Escolhe uma imagem PNG, JPEG ou WebP.',
  trainer_settings_logo_content_type_mismatch:
    'O conteúdo do ficheiro não corresponde ao formato indicado.',
  trainer_settings_logo_not_decodable: 'Não foi possível ler esta imagem.',
  trainer_settings_logo_dimensions_too_small: 'O logo tem de ter pelo menos 64 px de lado.',
  trainer_settings_logo_dimensions_too_large: 'O logo não pode ter mais de 4096 px de lado.',
  trainer_settings_logo_pixel_budget_exceeded: 'A imagem é demasiado grande. Usa uma mais pequena.',
  trainer_settings_logo_encoding_failed: 'Não foi possível processar esta imagem.',
  trainer_settings_logo_storage_unavailable:
    'O logo não pôde ser guardado agora. Nada foi alterado, tenta mais tarde.',
  trainer_settings_media_upload_failed:
    'O logo não pôde ser enviado. Nada foi alterado, tenta mais tarde.',
  trainer_settings_persistence_failed: 'Não foi possível guardar o logo. Tenta novamente.',
  logo_compensation_failed: 'Não foi possível guardar o logo. Tenta novamente.',
  rate_limit_exceeded: 'Foram feitos demasiados pedidos. Aguarda e tenta novamente.',
};

/**
 * Mensagem de uma falha das definições. Um erro de validação traz o código no campo
 * (`errors[].code`), não no `title`: procura-se primeiro aí.
 *
 * @param fallback Mensagem quando o código não é conhecido.
 */
export function settingsErrorMessage(error: unknown, fallback: string): string {
  if (!isApiProblem(error)) return 'Não foi possível contactar o servidor. Tenta novamente.';

  for (const fieldError of error.fieldErrors) {
    const message = SETTINGS_ERRORS[fieldError.code];
    if (message !== undefined) return message;
  }

  return SETTINGS_ERRORS[error.code] ?? fallback;
}

/**
 * Fusos IANA que o browser conhece, para o seletor. O fuso atual entra sempre, mesmo que o
 * browser não o liste, para o seletor nunca mostrar outro valor que não o guardado.
 */
export function timezoneOptions(current: string): string[] {
  const known = Intl.supportedValuesOf('timeZone');
  return known.includes(current) ? known : [current, ...known];
}
