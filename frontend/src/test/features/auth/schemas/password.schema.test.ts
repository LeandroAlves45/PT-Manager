import { describe, expect, it } from 'vitest';

import { loginSchema } from '@/features/auth/schemas/login.schema';
import {
  changePasswordSchema,
  inviteRegistrationSchema,
  newPasswordRule,
  resetPasswordSchema,
} from '@/features/auth/schemas/password.schema';
import { signUpSchema } from '@/features/auth/schemas/signup.schema';

function firstMessage(result: {
  success: boolean;
  error?: { issues: Array<{ message: string }> };
}) {
  return result.error?.issues[0]?.message;
}

describe('newPasswordRule', () => {
  it('accepts a password with upper, lower, digit and symbol', () => {
    expect(newPasswordRule.safeParse('Str0ng!Password').success).toBe(true);
  });

  it.each([
    ['Ab1!', 'A palavra-passe deve conter pelo menos 8 caracteres.'],
    ['semmaiuscula1!', 'Inclui pelo menos uma letra maiúscula.'],
    ['SEMMINUSCULA1!', 'Inclui pelo menos uma letra minúscula.'],
    ['SemNumero!!', 'Inclui pelo menos um número.'],
    ['SemSimbolo123', 'Inclui pelo menos um símbolo.'],
    // Letra acentuada não é símbolo para o Identity (`char.IsLetterOrDigit`).
    ['SemSimboloç123', 'Inclui pelo menos um símbolo.'],
    [`Aa1!${'x'.repeat(125)}`, 'A palavra-passe deve conter no máximo 128 caracteres.'],
  ])('rejects %j', (value, message) => {
    expect(firstMessage(newPasswordRule.safeParse(value))).toBe(message);
  });

  it('accepts exactly 8 and exactly 128 characters', () => {
    expect(newPasswordRule.safeParse('Aa1!aaaa').success).toBe(true);
    expect(newPasswordRule.safeParse(`Aa1!${'x'.repeat(124)}`).success).toBe(true);
  });
});

describe('confirmation schemas', () => {
  it('flags a differing confirmation on the confirmation field', () => {
    const result = resetPasswordSchema.safeParse({
      new_password: 'Str0ng!Password',
      confirm_new_password: 'Str0ng!Other',
    });

    expect(result.error?.issues[0]?.path).toEqual(['confirm_new_password']);
  });

  it('requires the current password on change and caps it at 512', () => {
    const base = { new_password: 'Str0ng!Password', confirm_new_password: 'Str0ng!Password' };

    expect(changePasswordSchema.safeParse({ ...base, current_password: '' }).success).toBe(false);
    expect(
      changePasswordSchema.safeParse({ ...base, current_password: 'x'.repeat(513) }).success
    ).toBe(false);
    expect(changePasswordSchema.safeParse({ ...base, current_password: 'x' }).success).toBe(true);
  });

  it('validates the invitation registration', () => {
    expect(
      inviteRegistrationSchema.safeParse({
        password: 'Str0ng!Password',
        confirm_password: 'Str0ng!Password',
      }).success
    ).toBe(true);
    expect(
      inviteRegistrationSchema.safeParse({ password: 'Str0ng!Password', confirm_password: '' })
        .success
    ).toBe(false);
  });
});

describe('signUpSchema', () => {
  it('trims the name and applies the password rule', () => {
    const result = signUpSchema.safeParse({
      full_name: '  Ana Sousa  ',
      email: 'ana@ptmanager.test',
      password: 'Str0ng!Password',
    });

    expect(result.data?.full_name).toBe('Ana Sousa');
    expect(
      signUpSchema.safeParse({ full_name: 'Ana', email: 'ana@ptmanager.test', password: 'fraca' })
        .success
    ).toBe(false);
  });
});

describe('loginSchema', () => {
  it('only requires a non-empty password up to 128 characters, not the composition rule', () => {
    expect(loginSchema.safeParse({ email: 'ana@ptmanager.test', password: 'a' }).success).toBe(
      true
    );
    expect(loginSchema.safeParse({ email: 'ana@ptmanager.test', password: '' }).success).toBe(
      false
    );
    expect(
      loginSchema.safeParse({ email: 'ana@ptmanager.test', password: 'x'.repeat(129) }).success
    ).toBe(false);
  });
});
