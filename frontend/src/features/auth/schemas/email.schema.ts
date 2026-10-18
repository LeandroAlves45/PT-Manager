import { z } from 'zod';

/** Formulários que só pedem o email: recuperar palavra-passe e reenviar confirmação. */
export const emailSchema = z.object({
  email: z.email('Introduz um email válido.'),
});

export type EmailFormValues = z.infer<typeof emailSchema>;
