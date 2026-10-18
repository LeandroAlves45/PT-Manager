import { z } from 'zod';

/**
 * Validação do formulário de login.
 *
 * Só exige que haja palavra-passe: a regra de composição (maiúscula, símbolo...) vale ao
 * criar ou mudar a palavra-passe, não ao entrar — uma conta antiga pode ter outra regra, e
 * recusar aqui esconderia o 401 verdadeiro. O limite de 128 espelha o servidor.
 */
export const loginSchema = z.object({
  email: z.email('Introduz um email válido.'),
  password: z
    .string()
    .min(1, 'Introduz a palavra-passe.')
    .max(128, 'A palavra-passe deve conter no máximo 128 caracteres.'),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
