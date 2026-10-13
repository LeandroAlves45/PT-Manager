import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { useUpdateProfileMutation } from '@/features/portal/api/portal';
import {
  profileFailure,
  profileSchema,
  toProfileUpdate,
  toProfileValues,
  type ProfileValues,
} from '@/features/portal/lib/profile';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

type Profile = components['schemas']['MyProfileResponse'];

/**
 * Contactos do cliente: telefone (obrigatório), email e contacto de emergência.
 *
 * O `PATCH` substitui os quatro campos de uma vez. Por isso o formulário parte sempre do
 * perfil lido e envia todos: um opcional vazio apaga o valor guardado.
 *
 * @param profile Perfil atual.
 */
export function ProfileContactsForm({ profile }: { profile: Profile }) {
  const mutation = useUpdateProfileMutation();
  const form = useForm<ProfileValues>({ defaultValues: toProfileValues(profile) });
  const errors = form.formState.errors;

  async function submit(values: ProfileValues) {
    const parsed = profileSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof ProfileValues, { message: issue.message });
      }
      return;
    }

    try {
      const saved = await mutation.mutateAsync(toProfileUpdate(parsed.data));
      form.reset(toProfileValues(saved));
      toast.success('Contactos guardados com sucesso.');
    } catch (failure) {
      const { field, message } = profileFailure(failure);
      form.setError(field, { message });
    }
  }

  return (
    <section
      aria-labelledby="contacts-title"
      className="border-border bg-card space-y-3 rounded-lg border p-3"
    >
      <div>
        <h2 id="contacts-title" className="font-display text-xl">
          Contactos
        </h2>
        <p className="text-muted-foreground text-sm">
          Só o telefone é obrigatório. Um campo vazio fica apagado.
        </p>
      </div>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => void submit(values))}
        className="space-y-4"
      >
        <FormField label="Telefone" error={errors.phone?.message}>
          {(control) => (
            <Input
              {...control}
              type="tel"
              autoComplete="tel"
              className="h-11"
              {...form.register('phone')}
            />
          )}
        </FormField>
        <FormField label="Email de contacto (opcional)" error={errors.contact_email?.message}>
          {(control) => (
            <Input
              {...control}
              type="email"
              autoComplete="email"
              className="h-11"
              {...form.register('contact_email')}
            />
          )}
        </FormField>
        <FormField
          label="Contacto de emergência: nome (opcional)"
          error={errors.emergency_contact_name?.message}
        >
          {(control) => (
            <Input
              {...control}
              autoComplete="off"
              className="h-11"
              {...form.register('emergency_contact_name')}
            />
          )}
        </FormField>
        <FormField
          label="Contacto de emergência: telefone (opcional)"
          error={errors.emergency_contact_phone?.message}
        >
          {(control) => (
            <Input
              {...control}
              type="tel"
              autoComplete="off"
              className="h-11"
              {...form.register('emergency_contact_phone')}
            />
          )}
        </FormField>
        {errors.root && (
          <p role="alert" className="text-destructive text-sm">
            {errors.root.message}
          </p>
        )}
        <Button type="submit" className="min-h-11 w-full" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Guardar contactos'}
        </Button>
      </form>
    </section>
  );
}
