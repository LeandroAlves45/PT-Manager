import { z } from 'zod';

/**
 * Validação do formulário de login.
 *
 * Espelha as regras conhecidas do backend (password de 8 a 128 caracteres), mas a
 * autoridade é sempre o servidor: isto serve para poupar uma ida à rede, não para decidir.
 */
export const loginSchema = z.object({
  email: z.email('Introduz um email válido.'),
  password: z
    .string()
    .min(8, 'A password deve conter pelo menos 8 caracteres.')
    .max(128, 'A password deve conter no máximo 128 caracteres.'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
