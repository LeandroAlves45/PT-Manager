import { describe, expect, it } from 'vitest';

import {
  AVATAR_MAX_BYTES,
  avatarFailureMessage,
  avatarFileError,
  isAcceptedEmail,
  profileFailure,
  profileInitials,
  profileSchema,
  toProfileUpdate,
  toProfileValues,
} from '@/features/portal/lib/profile';
import { toApiProblem } from '@/shared/api/problem';
import { portalProfile } from '@/test/msw/portal-fixtures';

/** Ficheiro com o tamanho indicado, sem alocar os bytes. */
function fileOf(type: string, size: number): File {
  const file = new File(['x'], 'foto', { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

describe('isAcceptedEmail', () => {
  it.each(['joao@ptmanager.local', 'a.b+c@sub.dominio.pt'])('accepts %s', (email) => {
    expect(isAcceptedEmail(email)).toBe(true);
  });

  // Os mesmos casos que o value object `EmailAddress` do backend recusa.
  it.each([
    'a@b',
    'a b@c.pt',
    'a@c.pt.',
    'a@.c.pt',
    'a@b@c.pt',
    'a@b.pt@c.pt',
    'sem-arroba',
    '@c.pt',
    'a@',
  ])('refuses %s', (email) => {
    expect(isAcceptedEmail(email)).toBe(false);
  });
});

describe('profileSchema', () => {
  const valid = {
    contact_email: '',
    phone: '+351912345678',
    emergency_contact_name: '',
    emergency_contact_phone: '',
  };

  it('accepts a phone alone and trims what was typed', () => {
    const parsed = profileSchema.safeParse({ ...valid, phone: '  912 345 678  ' });

    expect(parsed.success && parsed.data.phone).toBe('912 345 678');
  });

  it.each([
    [{ phone: '   ' }, 'Indica o telefone.'],
    [{ phone: '1'.repeat(33) }, 'O telefone não pode exceder 32 caracteres.'],
    [{ contact_email: 'a@b' }, 'Indica um email válido.'],
    [{ contact_email: `${'a'.repeat(251)}@c.pt` }, 'O email não pode exceder 255 caracteres.'],
    [{ emergency_contact_name: 'n'.repeat(256) }, 'O nome não pode exceder 255 caracteres.'],
    [{ emergency_contact_phone: '1'.repeat(33) }, 'O telefone não pode exceder 32 caracteres.'],
  ])('refuses %o', (change, message) => {
    const parsed = profileSchema.safeParse({ ...valid, ...change });

    expect(parsed.success).toBe(false);
    expect(parsed.error?.issues[0]?.message).toBe(message);
  });
});

describe('profile values', () => {
  it('shows a missing optional as empty and sends an empty optional as null', () => {
    const formValues = toProfileValues(portalProfile({ contact_email: null }));

    expect(formValues).toEqual({
      contact_email: '',
      phone: '+351912345678',
      emergency_contact_name: '',
      emergency_contact_phone: '',
    });
    expect(toProfileUpdate(formValues)).toEqual({
      contact_email: null,
      phone: '+351912345678',
      emergency_contact_name: null,
      emergency_contact_phone: null,
    });
  });

  it('keeps the filled optionals', () => {
    expect(
      toProfileUpdate({
        contact_email: 'joao@ptmanager.local',
        phone: '912345678',
        emergency_contact_name: 'Ana Pereira',
        emergency_contact_phone: '961112223',
      })
    ).toEqual({
      contact_email: 'joao@ptmanager.local',
      phone: '912345678',
      emergency_contact_name: 'Ana Pereira',
      emergency_contact_phone: '961112223',
    });
  });
});

describe('profileInitials', () => {
  it.each([
    ['João Pereira', 'JP'],
    ['  maria  de  lurdes  silva ', 'MS'],
    ['Marta', 'M'],
    ['', ''],
  ])('reads "%s" as "%s"', (name, initials) => {
    expect(profileInitials(name)).toBe(initials);
  });
});

describe('profileFailure', () => {
  it('reads a validation code from errors[] and points at its field', () => {
    const failure = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'ContactEmail', code: 'client_email_invalid', message: 'x' }],
    });

    expect(profileFailure(failure)).toEqual({
      field: 'contact_email',
      message: 'Indica um email válido.',
    });
  });

  it.each([
    [
      'client_email_already_exists',
      'contact_email',
      'Este email já está a ser usado noutra ficha de cliente.',
    ],
    [
      'client_phone_already_exists',
      'phone',
      'Este telefone já está a ser usado noutra ficha de cliente.',
    ],
  ] as const)('points the 409 %s at its field', (code, field, message) => {
    expect(profileFailure(toApiProblem(409, { title: code }))).toEqual({ field, message });
  });

  it('puts an archived profile and anything unknown on the form, not on a field', () => {
    expect(profileFailure(toApiProblem(404, { title: 'portal_profile_not_available' }))).toEqual({
      field: 'root',
      message: 'A tua ficha não está ativa. Fala com o teu personal trainer.',
    });
    expect(profileFailure(toApiProblem(500, { title: 'constructor' })).field).toBe('root');
    expect(profileFailure(new TypeError('Failed to fetch'))).toEqual({
      field: 'root',
      message: 'Não foi possível guardar os contactos. Tenta novamente.',
    });
  });
});

describe('avatarFileError', () => {
  it('uses the 4 MiB limit of the backend image profile', () => {
    expect(AVATAR_MAX_BYTES).toBe(4_194_304);
  });

  it.each(['image/png', 'image/jpeg', 'image/webp'])('accepts %s up to 4 MiB', (type) => {
    expect(avatarFileError(fileOf(type, AVATAR_MAX_BYTES))).toBeNull();
  });

  it('refuses another format, an empty file and one above 4 MiB', () => {
    expect(avatarFileError(fileOf('image/gif', 10))).toBe('Escolhe uma imagem PNG, JPEG ou WebP.');
    expect(avatarFileError(fileOf('image/png', 0))).toBe('O ficheiro está vazio.');
    expect(avatarFileError(fileOf('image/png', AVATAR_MAX_BYTES + 1))).toBe(
      'A fotografia não pode exceder 4 MB.'
    );
  });
});

describe('avatarFailureMessage', () => {
  const fallback = 'Não foi possível atualizar a fotografia.';

  it('reads the moderation refusal from errors[]', () => {
    const failure = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'Avatar', code: 'portal_avatar_content_not_allowed', message: 'x' }],
    });

    expect(avatarFailureMessage(failure, fallback)).toBe(
      'Esta imagem não foi aceite. Escolhe outra fotografia.'
    );
  });

  it.each(['portal_avatar_moderation_unavailable', 'portal_avatar_storage_unavailable'])(
    'reads the 503 %s from the title',
    (code) => {
      expect(avatarFailureMessage(toApiProblem(503, { title: code }), fallback)).toBe(
        'Não é possível enviar fotografias neste momento. Nada foi alterado. Tenta mais tarde.'
      );
    }
  );

  it('recognises the upload limit by the 429 status, not by the title', () => {
    expect(avatarFailureMessage(toApiProblem(429, { title: 'Too many requests' }), fallback)).toBe(
      'Enviaste demasiadas fotografias. Tenta de novo dentro de 1 hora.'
    );
  });

  it.each([
    ['portal_avatar_dimensions_too_small', 'A imagem tem de ter pelo menos 64 px de lado.'],
    [
      'portal_avatar_pixel_budget_exceeded',
      'A imagem é demasiado grande. Usa uma com menos de 4096 px de lado.',
    ],
    ['portal_avatar_not_decodable', 'Escolhe uma imagem PNG, JPEG ou WebP válida.'],
    ['portal_avatar_media_too_large', 'A fotografia não pode exceder 4 MB.'],
  ])('explains %s', (code, message) => {
    const failure = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'Avatar', code, message: 'x' }],
    });

    expect(avatarFailureMessage(failure, fallback)).toBe(message);
  });

  it('falls back for an unknown code and for a network error', () => {
    expect(avatarFailureMessage(toApiProblem(500, { title: 'constructor' }), fallback)).toBe(
      fallback
    );
    expect(avatarFailureMessage(new TypeError('Failed to fetch'), fallback)).toBe(fallback);
  });
});
