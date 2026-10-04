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
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

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
        .min(1, 'Indica o dia do check-in do cliente.')
        .refine((value) => value >= minimumDate, minimumMessage),
      target_date: z.string(),
    })
    .refine((values) => values.target_date === '' || values.target_date >= values.check_in_date, {
      path: ['target_date'],
      message: 'A data-alvo não pode ser anterior ao check-in.',
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
};

/**
 * Formulário "Agendar check-in" (sem `checkIn`) ou "Reagendar" (com `checkIn`).
 *
 * - Agendar: cliente (fixo na tab do cliente), dia e data-alvo opcional (a data até à qual
 *   o cliente deve atingir o objetivo do check-in).
 * - Reagendar: dia e data-alvo, pré-preenchidos.
 *
 * A data mínima usa o dia do browser; o servidor decide com o fuso do personal trainer e,
 * se discordar, a recusa aparece no campo do dia.
 *
 * @param client Cliente fixo (tab do cliente) ou `null` para o escolher.
 * @param checkIn Check-in a reagendar, ou `undefined` para agendar um novo.
 * @param onSaved Chamado depois de guardar.
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
  const form = useForm<ScheduleValues>({
    defaultValues: {
      client: checkIn !== undefined ? { id: checkIn.client_id, name: checkIn.client_name } : client,
      check_in_date: checkIn?.check_in_date ?? '',
      target_date: checkIn?.target_date ?? '',
    },
  });
  const errors = form.formState.errors;

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
        const saved = await create.mutateAsync({
          client_id: values.client!.id,
          check_in_date: values.check_in_date,
          target_date: targetDate,
        });
        toast.success(`Check-in de ${saved.client_name} agendado.`);
      } else {
        await reschedule.mutateAsync({
          checkInId: checkIn.id,
          body: { check_in_date: values.check_in_date, target_date: targetDate },
        });
        toast.success(`Check-in de ${checkIn.client_name} reagendado.`);
      }
      onSaved();
    } catch (error) {
      const fallback = 'Não foi possível guardar o check-in. Tenta novamente.';
      if (!isApiProblem(error)) {
        form.setError('root', { message: fallback });
        return;
      }
      const conflictField = CONFLICT_FIELDS[error.code];
      if (conflictField !== undefined) {
        form.setError(conflictField, { message: checkInErrorMessage(error.code, fallback) });
        return;
      }

      let mapped = false;
      for (const fieldError of error.fieldErrors) {
        const field = SERVER_FIELDS[fieldError.field];
        if (field !== undefined) {
          form.setError(field, { message: checkInErrorMessage(fieldError.code, fallback) });
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
      {client === null && checkIn === undefined && (
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
        <FormField label="Dia do check-in" error={errors.check_in_date?.message}>
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
    </form>
  );
}
