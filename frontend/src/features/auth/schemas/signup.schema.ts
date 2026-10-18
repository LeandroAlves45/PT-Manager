import { z } from 'zod';

import { newPasswordRule } from '@/features/auth/schemas/password.schema';

/** Criação de conta de personal trainer. Limites do servidor: nome 255, email 255. */
export const signUpSchema = z.object({
  full_name: z
    .string()
    .trim()
    .min(1, 'Indica o teu nome completo.')
    .max(255, 'O nome deve conter no máximo 255 caracteres.')
    // O backend recusa U+0000 e controlos (D1); aqui falha-se antes do pedido, com texto claro.
    // eslint-disable-next-line no-control-regex
    .refine((value) => !/[\u0000-\u001F\u007F]/.test(value), 'O nome tem caracteres inválidos.'),
  email: z.email('Introduz um email válido.').max(255, 'O email é demasiado longo.'),
  password: newPasswordRule,
});

export type SignUpFormValues = z.infer<typeof signUpSchema>;
