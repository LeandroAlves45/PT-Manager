import { format } from 'date-fns';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';
import { toast } from 'sonner';
import { z } from 'zod';

import { useSaveClientMutation } from '@/features/clients/api/mutations';
import { FormField } from '@/features/clients/components/FormField';
import { CLIENT_CAPACITY_MESSAGES, SEX_LABELS } from '@/features/clients/lib/labels';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Textarea } from '@/shared/components/ui/textarea';

type ClientDetails = components['schemas']['ClientDetailsResponse'];

/** Texto opcional com o limite do backend; vazio vira 'null' no pedido. */
const optionalText = (max: number, message: string) => z.string().trim().max(max, message);

/**
 * Regras iguais a `CreateClientCommandValidator`/`UpdateClientCommandValidator` e às
 * colunas de `ClientConfiguration` (nome/email/objetivo 255, telefones 32). Mensagens em
 * PT-PT explícitas: o Zod 4 não as traduz (defeito D4 da 6D).
 */
const clientSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Indica o nome do cliente.')
    .max(255, 'O nome não pode ter exceder 255 caracteres.'),
  contact_email: z
    .string()
    .trim()
    .max(255, 'O email não pode ter exceder 255 caracteres.')
    .refine(
      (value) => value === '' || z.email().safeParse(value).success,
      'Indica um email válido.'
    ),
  phone: z
    .string()
    .trim()
    .min(1, 'Indica um número de telefone.')
    .max(32, 'O número de telefone não pode ter exceder 32 caracteres.'),
  birth_date: z
    .string()
    .min(1, 'Indica a data de nascimento.')
    .refine(
      (value) => value <= format(new Date(), 'yyyy-MM-dd'),
      'A data de nascimento não pode ser no futuro.'
    ),
  sex: z.enum(['male', 'female'], { error: 'Escolhe o sexo biológico.' }),
  objective: optionalText(255, 'O objetivo não pode exceder 255 caracteres.'),
  notes: z.string(),
  emergency_contact_name: optionalText(255, 'O nome não pode exceder 255 caracteres.'),
  emergency_contact_phone: optionalText(32, 'O número de telefone não pode exceder 32 caracteres.'),
});
type ClientValues = z.input<typeof clientSchema>;

/** Campos do backend para os campos do formulário. */
const SERVER_FIELDS: Record<string, keyof ClientValues> = {
  Name: 'name',
  ContactEmail: 'contact_email',
  Phone: 'phone',
  BirthDate: 'birth_date',
  Sex: 'sex',
  Objective: 'objective',
  EmergencyContactName: 'emergency_contact_name',
  EmergencyContactPhone: 'emergency_contact_phone',
};

/** Conflitos 409 que pertencem a um campo concreto. */
const CONFLICT_FIELDS: Record<string, { field: keyof ClientValues; message: string }> = {
  client_email_already_exists: {
    field: 'contact_email',
    message: 'Já existe um cliente com este email.',
  },
  client_phone_already_exists: {
    field: 'phone',
    message: 'Já existe um cliente com este telefone.',
  },
};

const NULLABLE = [
  'contact_email',
  'objective',
  'notes',
  'emergency_contact_name',
  'emergency_contact_phone',
] as const;

/**
 * Formulário da ficha do cliente (criar e editar).
 *
 * @param client Ficha a editar, ou `null` para criar.
 * @param onSaved Chamado com a ficha devolvida pela API depois de guardar.
 */
export function ClientForm({
  client,
  onSaved,
}: {
  client: ClientDetails | null;
  onSaved: (client: ClientDetails) => void;
}) {
  const mutation = useSaveClientMutation(client?.id ?? null);
  const form = useForm<ClientValues>({
    defaultValues: {
      name: client?.name ?? '',
      contact_email: client?.contact_email ?? '',
      phone: client?.phone ?? '',
      birth_date: client?.birth_date ?? '',
      sex: (client?.sex as ClientValues['sex'] | undefined) ?? 'female',
      objective: client?.objective ?? '',
      notes: client?.notes ?? '',
      emergency_contact_name: client?.emergency_contact_name ?? '',
      emergency_contact_phone: client?.emergency_contact_phone ?? '',
    },
  });

  const errors = form.formState.errors;
  const subscriptionMessage = errors.root?.type === 'subscription' ? errors.root.message : null;

  async function submit(values: ClientValues) {
    const parsed = clientSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof ClientValues, { message: issue.message });
      }
      return;
    }

    const body = { ...parsed.data };
    const request = {
      ...body,
      ...Object.fromEntries(NULLABLE.map((key) => [key, body[key].trim() || null])),
    } as components['schemas']['CreateClientRequest'];

    try {
      const saved = await mutation.mutateAsync(request);
      toast.success(
        client === null ? 'Cliente criado com sucesso.' : 'Cliente atualizado com sucesso.'
      );
      onSaved(saved);
    } catch (error) {
      if (!isApiProblem(error)) {
        form.setError('root', { message: 'Não foi possível guardar. Tenta novamente.' });
        return;
      }

      const conflict = CONFLICT_FIELDS[error.code];
      const subscription = CLIENT_CAPACITY_MESSAGES[error.code];
      if (conflict !== undefined) {
        form.setError(conflict.field, { message: conflict.message });
      } else if (subscription !== undefined) {
        form.setError('root', { type: 'subscription', message: subscription });
      } else if (error.hasFieldErrors) {
        for (const fieldError of error.fieldErrors) {
          const field = SERVER_FIELDS[fieldError.field];
          if (field !== undefined) form.setError(field, { message: fieldError.message });
        }
      } else {
        form.setError('root', { message: 'Não foi possível guardar. Tenta novamente.' });
      }
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      <FormField label="Nome" error={errors.name?.message}>
        {(control) => <Input {...control} autoComplete="off" {...form.register('name')} />}
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Email de contacto" error={errors.contact_email?.message}>
          {(control) => <Input {...control} type="email" {...form.register('contact_email')} />}
        </FormField>
        <FormField label="Telefone" error={errors.phone?.message}>
          {(control) => <Input {...control} type="tel" {...form.register('phone')} />}
        </FormField>
        <FormField label="Data de nascimento" error={errors.birth_date?.message}>
          {(control) => <Input {...control} type="date" {...form.register('birth_date')} />}
        </FormField>
        <FormField label="Sexo biológico" error={errors.sex?.message}>
          {(control) => (
            <NativeSelect {...control} {...form.register('sex')}>
              {Object.entries(SEX_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </NativeSelect>
          )}
        </FormField>
      </div>
      <FormField label="Objetivo" error={errors.objective?.message}>
        {(control) => <Input {...control} {...form.register('objective')} />}
      </FormField>
      <FormField label="Notas" error={errors.notes?.message}>
        {(control) => <Textarea {...control} rows={3} {...form.register('notes')} />}
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Contacto de emergência" error={errors.emergency_contact_name?.message}>
          {(control) => <Input {...control} {...form.register('emergency_contact_name')} />}
        </FormField>
        <FormField label="Telefone de emergência" error={errors.emergency_contact_phone?.message}>
          {(control) => (
            <Input {...control} type="tel" {...form.register('emergency_contact_phone')} />
          )}
        </FormField>
      </div>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}{' '}
          {subscriptionMessage !== null && (
            <Link to="/trainer/billing" className="text-foreground underline underline-offset-4">
              Ver subscrição
            </Link>
          )}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending
            ? 'A guardar…'
            : client === null
              ? 'Criar cliente'
              : 'Guardar alterações'}
        </Button>
      </div>
    </form>
  );
}
