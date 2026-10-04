import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useUpdateContactsMutation } from '@/features/trainer-settings/api/settings';
import { settingsErrorMessage } from '@/features/trainer-settings/lib/settings';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';

type Settings = components['schemas']['TrainerSettingsResponse'];

/** Regras de `UpdateContactsCommandValidator`: telefone ≤ 20, morada ≤ 500, cidade ≤ 255. */
const contactsSchema = z.object({
  phone: z.string().trim().max(20, 'O telefone não pode exceder 20 caracteres.'),
  address: z.string().trim().max(500, 'A morada não pode exceder 500 caracteres.'),
  city: z.string().trim().max(255, 'A cidade não pode exceder 255 caracteres.'),
});
type ContactsValues = z.input<typeof contactsSchema>;

const SERVER_FIELDS: Readonly<Record<string, keyof ContactsValues>> = {
  Phone: 'phone',
  Address: 'address',
  City: 'city',
};

const orNull = (value: string) => (value === '' ? null : value);

function toValues(settings: Settings): ContactsValues {
  return {
    phone: settings.phone ?? '',
    address: settings.address ?? '',
    city: settings.city ?? '',
  };
}

/**
 * Contactos opcionais. O `PATCH` substitui os três: um campo vazio apaga o valor guardado.
 *
 * @param settings Definições atuais.
 */
export function ContactsSection({ settings }: { settings: Settings }) {
  const mutation = useUpdateContactsMutation();
  const form = useForm<ContactsValues>({ defaultValues: toValues(settings) });
  const errors = form.formState.errors;

  async function submit(values: ContactsValues) {
    const parsed = contactsSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof ContactsValues, { message: issue.message });
      }
      return;
    }

    try {
      const saved = await mutation.mutateAsync({
        phone: orNull(parsed.data.phone),
        address: orNull(parsed.data.address),
        city: orNull(parsed.data.city),
      });
      form.reset(toValues(saved));
      toast.success('Contactos guardados com sucesso.');
    } catch (error) {
      const message = settingsErrorMessage(error, 'Não foi possível guardar os contactos.');
      const field = isApiProblem(error)
        ? error.fieldErrors.map((item) => SERVER_FIELDS[item.field]).find(Boolean)
        : undefined;
      form.setError(field ?? 'root', { message });
    }
  }

  return (
    <section
      aria-labelledby="contacts-title"
      className="border-border space-y-4 rounded-xl border p-5"
    >
      <div>
        <h2 id="contacts-title" className="font-display text-xl">
          Contactos
        </h2>
        <p className="text-muted-foreground text-sm">Opcionais. Campos vazios ficam apagados.</p>
      </div>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => void submit(values))}
        className="space-y-4"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label="Telefone" error={errors.phone?.message}>
            {(control) => <Input {...control} type="tel" {...form.register('phone')} />}
          </FormField>
          <FormField label="Cidade" error={errors.city?.message}>
            {(control) => <Input {...control} {...form.register('city')} />}
          </FormField>
        </div>
        <FormField label="Morada" error={errors.address?.message}>
          {(control) => <Textarea {...control} rows={2} {...form.register('address')} />}
        </FormField>
        {errors.root && (
          <p role="alert" className="text-destructive text-sm">
            {errors.root.message}
          </p>
        )}
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Guardar contactos'}
        </Button>
      </form>
    </section>
  );
}
