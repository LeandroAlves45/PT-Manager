import { z } from 'zod';

import { newPasswordRule } from '@/features/auth/schemas/password.schema';

/** Criação de conta de personal trainer. Limites do servidor: nome 255, email 255. */
export const signUpSchema = z.object({
  full_name: z
    .string()
    .trim()
    .min(1, 'Indica o teu nome completo.')
    .max(255, 'O nome deve conter no máximo 255 caracteres.'),
  email: z.email('Introduz um email válido.').max(255, 'O email é demasiado longo.'),
  password: newPasswordRule,
});

export type SignUpFormValues = z.infer<typeof signUpSchema>;
