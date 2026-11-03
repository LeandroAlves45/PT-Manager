import { describe, expect, it } from 'vitest';

import { profileInitials } from '@/features/portal/lib/initials';
import {
  AVATAR_MAX_BYTES,
  avatarFailureMessage,
  avatarFileError,
  profileFailure,
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

describe('profileSchema', () => {
  const valid = {
    name: 'João Pereira',
    phone: '+351912345678',
    emergency_contact_name: '',
    emergency_contact_phone: '',
  };

  it('accepts a name and a phone, and trims what was typed', () => {
    const parsed = profileSchema.safeParse({
      ...valid,
      name: '  Joana Silva  ',
      phone: '  912 345 678  ',
    });

    expect(parsed.success && parsed.data.name).toBe('Joana Silva');
    expect(parsed.success && parsed.data.phone).toBe('912 345 678');
  });

  it.each([
    [{ name: '   ' }, 'Indica o teu nome.'],
    [{ name: 'n'.repeat(256) }, 'O nome não pode exceder 255 caracteres.'],
    [{ name: 'Jo\u0000ão' }, 'O nome tem caracteres inválidos.'],
    [{ phone: '   ' }, 'Indica o telefone.'],
    [{ phone: '1'.repeat(33) }, 'O telefone não pode exceder 32 caracteres.'],
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
    const formValues = toProfileValues(portalProfile());

    expect(formValues).toEqual({
      name: 'João Pereira',
      phone: '+351912345678',
      emergency_contact_name: '',
      emergency_contact_phone: '',
    });
    expect(toProfileUpdate(formValues)).toEqual({
      name: 'João Pereira',
      phone: '+351912345678',
      emergency_contact_name: null,
      emergency_contact_phone: null,
    });
  });

  it('keeps the filled optionals and never carries an email', () => {
    const update = toProfileUpdate({
      name: 'Joana Silva',
      phone: '912345678',
      emergency_contact_name: 'Ana Pereira',
      emergency_contact_phone: '961112223',
    });

    expect(update).toEqual({
      name: 'Joana Silva',
      phone: '912345678',
      emergency_contact_name: 'Ana Pereira',
      emergency_contact_phone: '961112223',
    });
    expect(update).not.toHaveProperty('contact_email');
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
      errors: [{ field: 'Name', code: 'client_name_invalid', message: 'x' }],
    });

    expect(profileFailure(failure)).toEqual({
      field: 'name',
      message: 'Indica o teu nome, com até 255 caracteres.',
    });
  });

  it('points a generic code at the field the server names', () => {
    const failure = toApiProblem(400, {
      title: 'validation_failed',
      errors: [{ field: 'EmergencyContactName', code: 'text_invalid_character', message: 'x' }],
    });

    expect(profileFailure(failure)).toEqual({
      field: 'emergency_contact_name',
      message: 'O texto tem caracteres inválidos.',
    });
  });

  it('points the 409 of the phone at its field', () => {
    expect(profileFailure(toApiProblem(409, { title: 'client_phone_already_exists' }))).toEqual({
      field: 'phone',
      message: 'Este telefone já está a ser usado noutra ficha de cliente.',
    });
  });

  it('puts an archived profile and anything unknown on the form, not on a field', () => {
    expect(profileFailure(toApiProblem(404, { title: 'portal_profile_not_available' }))).toEqual({
      field: 'root',
      message: 'A tua ficha não está ativa. Fala com o teu personal trainer.',
    });
    expect(profileFailure(toApiProblem(500, { title: 'constructor' })).field).toBe('root');
    expect(profileFailure(new TypeError('Failed to fetch'))).toEqual({
      field: 'root',
      message: 'Não foi possível guardar os dados. Tenta novamente.',
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
