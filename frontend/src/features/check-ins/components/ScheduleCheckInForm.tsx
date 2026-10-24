import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import {
  useCreateCheckInMutation,
  useRescheduleCheckInMutation,
} from '@/features/check-ins/api/mutations';
import { checkInErrorMessage } from '@/features/check-ins/lib/checkInStatus';
import { todayKey, tomorrowKey } from '@/features/check-ins/lib/dates';
import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';

type CheckIn = components['schemas']['CheckInResponse'];

/**
 * Regras de `CreateCheckInCommandValidator`/`RescheduleCheckInCommandValidator` e do store:
 * dia obrigatório, data-alvo opcional e nunca anterior ao dia. Agendar aceita hoje;
 * reagendar só um dia futuro (`CheckIn.Reschedule`).
 */
function scheduleSchema(minimumDate: string, minimumMessage: string) {
  return z
    .object({
      client: z.custom<ClientChoice | null>().refine((value) => value !== null, {
        message: 'Escolhe o cliente.',
      }),
      check_in_date: z
        .string()
        .min(1, 'Indica o dia da avaliação.')
        .refine((value) => value >= minimumDate, minimumMessage),
      target_date: z.string(),
    })
    .refine((values) => values.target_date === '' || values.target_date >= values.check_in_date, {
      path: ['target_date'],
      message: 'A data-alvo não pode ser anterior à avaliação.',
    });
}

interface ScheduleValues {
  client: ClientChoice | null;
  check_in_date: string;
  target_date: string;
}

/** Campos do servidor (PascalCase do comando) para os campos do formulário. */
const SERVER_FIELDS: Readonly<Record<string, keyof ScheduleValues>> = {
  ClientId: 'client',
  CheckInDate: 'check_in_date',
  TargetDate: 'target_date',
};

/** Recusas de regra que pertencem a um campo e não ao formulário inteiro. */
const CONFLICT_FIELDS: Readonly<Record<string, keyof ScheduleValues>> = {
  check_in_date_conflict: 'check_in_date',
  check_in_date_not_allowed: 'check_in_date',
  check_in_cannot_be_rescheduled: 'check_in_date',
  assessment_client_inactive: 'client',
  initial_assessment_required: 'client',
};

/**
 * Formulário "Agendar avaliação" (sem `checkIn`) ou "Reagendar" (com `checkIn`).
 *
 * - Agendar: cliente (fixo na tab do cliente), dia e data-alvo opcional (a data até à qual
 *   o cliente deve atingir o objetivo da avaliação).
 * - Reagendar: dia e data-alvo, pré-preenchidos.
 *
 * A data mínima usa o dia do browser; o servidor decide com o fuso do personal trainer e,
 * se discordar, a recusa aparece no campo do dia.
 *
 * Sair com alterações por guardar pede confirmação; o guard só deixa de valer depois de
 * `saved` estar a `true`, e é aí que `onSaved` navega.
 *
 * @param client Cliente fixo (aberto a partir da ficha) ou `null` para o escolher.
 * @param checkIn Avaliação a reagendar, ou `undefined` para agendar uma nova.
 * @param onSaved Chamado depois de guardar, já sem alterações por guardar.
 */
export function ScheduleCheckInForm({
  client,
  checkIn,
  onSaved,
}: {
  client: ClientChoice | null;
  checkIn?: CheckIn;
  onSaved: () => void;
}) {
  const create = useCreateCheckInMutation();
  const reschedule = useRescheduleCheckInMutation();
  const pending = create.isPending || reschedule.isPending;
  // Só se escolhe o cliente ao agendar a partir da página global.
  const pickClient = client === null && checkIn === undefined;
  const form = useForm<ScheduleValues>({
    defaultValues: {
      client: checkIn !== undefined ? { id: checkIn.client_id, name: checkIn.client_name } : client,
      check_in_date: checkIn?.check_in_date ?? '',
      target_date: checkIn?.target_date ?? '',
    },
  });
  const errors = form.formState.errors;
  const [saved, setSaved] = useState(false);
  const blocker = useUnsavedChangesGuard(form.formState.isDirty && !saved);
  useEffect(() => {
    if (saved) onSaved();
  }, [saved, onSaved]);

  // Datas `yyyy-MM-dd` comparam-se como texto: agendar aceita hoje, reagendar só amanhã.
  const schema =
    checkIn === undefined
      ? scheduleSchema(todayKey(), 'Escolhe hoje ou um dia futuro.')
      : scheduleSchema(tomorrowKey(), 'Escolhe um dia depois de hoje.');

  async function submit(values: ScheduleValues) {
    const parsed = schema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof ScheduleValues, { message: issue.message });
      }
      return;
    }

    const targetDate = values.target_date === '' ? null : values.target_date;
    try {
      if (checkIn === undefined) {
        const created = await create.mutateAsync({
          client_id: values.client!.id,
          check_in_date: values.check_in_date,
          target_date: targetDate,
        });
        toast.success(`Avaliação periódica de ${created.client_name} agendada.`);
      } else {
        await reschedule.mutateAsync({
          checkInId: checkIn.id,
          body: { check_in_date: values.check_in_date, target_date: targetDate },
        });
        toast.success(`Avaliação periódica de ${checkIn.client_name} reagendada.`);
      }
      setSaved(true);
    } catch (error) {
      const fallback = 'Não foi possível guardar a avaliação. Tenta novamente.';
      if (!isApiProblem(error)) {
        form.setError('root', { message: fallback });
        return;
      }
      // Com o cliente fixo (tab do cliente ou reagendar) o campo não existe: o erro vai para o
      // formulário, senão ficava num campo que ninguém vê.
      const shown = (field: keyof ScheduleValues) =>
        field === 'client' && !pickClient ? 'root' : field;

      const conflictField = CONFLICT_FIELDS[error.code];
      if (conflictField !== undefined) {
        form.setError(shown(conflictField), {
          message: checkInErrorMessage(error.code, fallback),
        });
        return;
      }

      let mapped = false;
      for (const fieldError of error.fieldErrors) {
        const field = SERVER_FIELDS[fieldError.field];
        if (field !== undefined) {
          form.setError(shown(field), { message: checkInErrorMessage(fieldError.code, fallback) });
          mapped = true;
        }
      }
      if (!mapped) form.setError('root', { message: checkInErrorMessage(error.code, fallback) });
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      {pickClient && (
        <FormField label="Cliente" error={errors.client?.message}>
          {(control) => (
            <Controller
              control={form.control}
              name="client"
              render={({ field }) => (
                <ClientCombobox {...control} value={field.value} onChange={field.onChange} />
              )}
            />
          )}
        </FormField>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Dia da avaliação" error={errors.check_in_date?.message}>
          {(control) => <Input {...control} type="date" {...form.register('check_in_date')} />}
        </FormField>
        <FormField label="Data-alvo (opcional)" error={errors.target_date?.message}>
          {(control) => <Input {...control} type="date" {...form.register('target_date')} />}
        </FormField>
      </div>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={pending}>
          {pending ? 'A guardar…' : checkIn === undefined ? 'Agendar' : 'Reagendar'}
        </Button>
      </div>
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem guardar?"
        description="Os dados desta avaliação perdem-se."
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </form>
  );
}
