import { z } from 'zod';

/** Texto de ajuda mostrado por baixo de qualquer campo de palavra-passe nova. */
export const PASSWORD_RULE_HINT =
  'Entre 8 e 128 caracteres, com maiúscula, minúscula, número e símbolo.';

/**
 * Regra de palavra-passe do ASP.NET Identity tal como o backend a configura: 8 a 128
 * caracteres, com maiúscula, minúscula, dígito e símbolo (carácter não alfanumérico).
 *
 * Espelha o servidor só para poupar uma ida à rede; quem decide é o servidor
 * (`authentication_new_password_rejected`).
 */
export const newPasswordRule = z
  .string()
  .min(8, 'A palavra-passe deve conter pelo menos 8 caracteres.')
  .max(128, 'A palavra-passe deve conter no máximo 128 caracteres.')
  .regex(/[A-Z]/, 'Inclui pelo menos uma letra maiúscula.')
  .regex(/[a-z]/, 'Inclui pelo menos uma letra minúscula.')
  .regex(/\d/, 'Inclui pelo menos um número.')
  // Símbolo = `!char.IsLetterOrDigit` do Identity (Unicode): "ç" é letra, não conta.
  .regex(/[^\p{L}\p{Nd}]/u, 'Inclui pelo menos um símbolo.');

const MISMATCH = 'As palavras-passe não coincidem.';

/** Repor a palavra-passe com o token do email. */
export const resetPasswordSchema = z
  .object({
    new_password: newPasswordRule,
    confirm_new_password: z.string().min(1, 'Confirma a nova palavra-passe.'),
  })
  .refine((values) => values.new_password === values.confirm_new_password, {
    message: MISMATCH,
    path: ['confirm_new_password'],
  });

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

/** Mudar a palavra-passe com sessão iniciada. */
export const changePasswordSchema = z
  .object({
    current_password: z
      .string()
      .min(1, 'Introduz a palavra-passe atual.')
      .max(512, 'A palavra-passe atual é demasiado longa.'),
    new_password: newPasswordRule,
    confirm_new_password: z.string().min(1, 'Confirma a nova palavra-passe.'),
  })
  .refine((values) => values.new_password === values.confirm_new_password, {
    message: MISMATCH,
    path: ['confirm_new_password'],
  });

export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;

/** Criar a conta de cliente a partir de um convite. */
export const inviteRegistrationSchema = z
  .object({
    password: newPasswordRule,
    confirm_password: z.string().min(1, 'Confirma a palavra-passe.'),
  })
  .refine((values) => values.password === values.confirm_password, {
    message: MISMATCH,
    path: ['confirm_password'],
  });

export type InviteRegistrationFormValues = z.infer<typeof inviteRegistrationSchema>;
