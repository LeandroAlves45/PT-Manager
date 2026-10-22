import { CheckCircle2 } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { useUpdateProfileMutation } from '@/features/portal/api/portal';
import { PortalCard } from '@/features/portal/components/PortalCard';
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
 * Dados editáveis do cliente: nome, telefone (obrigatórios) e contacto de emergência.
 *
 * O `PATCH` substitui os quatro campos de uma vez. Por isso o formulário parte sempre do
 * perfil lido e envia todos: um opcional vazio apaga o valor guardado. O email da ficha não
 * se vê nem se edita aqui.
 *
 * Depois de gravar, além do aviso passageiro, o cartão mostra «Dados guardados.» até o
 * cliente voltar a alterar um campo.
 *
 * @param profile Perfil atual.
 */
export function ProfileContactsForm({ profile }: { profile: Profile }) {
  const mutation = useUpdateProfileMutation();
  const form = useForm<ProfileValues>({ defaultValues: toProfileValues(profile) });
  const [saved, setSaved] = useState(false);
  const errors = form.formState.errors;

  async function submit(values: ProfileValues) {
    setSaved(false);

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
      const updated = await mutation.mutateAsync(toProfileUpdate(parsed.data));
      form.reset(toProfileValues(updated));
      setSaved(true);
      toast.success('Dados guardados com sucesso.');
    } catch (failure) {
      const { field, message } = profileFailure(failure);
      form.setError(field, { message });
    }
  }

  return (
    <PortalCard title="Os teus dados">
      <p className="text-muted-foreground mb-3 text-sm">
        O nome e o telefone são obrigatórios. Um campo opcional vazio fica apagado.
      </p>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => void submit(values))}
        // A confirmação só vale enquanto nada mudou: escrever num campo apaga-a. O `reset`
        // que segue a gravação não dispara eventos do DOM, por isso não a apaga.
        onChange={() => setSaved(false)}
        className="space-y-4"
      >
        <FormField label="Nome" error={errors.name?.message}>
          {(control) => (
            <Input {...control} autoComplete="name" className="h-11" {...form.register('name')} />
          )}
        </FormField>
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
          {mutation.isPending ? 'A guardar…' : 'Guardar dados'}
        </Button>
        {/* Região sempre presente para o leitor de ecrã anunciar a confirmação quando entra. */}
        <p role="status" className="flex items-center gap-2 text-sm empty:hidden">
          {saved && (
            <>
              <CheckCircle2 aria-hidden className="text-primary size-5 shrink-0" />
              Dados guardados.
            </>
          )}
        </p>
      </form>
    </PortalCard>
  );
}
