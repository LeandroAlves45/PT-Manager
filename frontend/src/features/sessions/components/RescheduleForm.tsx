import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { useRescheduleSessionMutation } from '@/features/sessions/api/mutations';
import { localInstant, splitStartsAt, toStartsAt } from '@/features/sessions/lib/dates';
import { SESSION_ERRORS } from '@/features/sessions/lib/sessionStatus';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

type Session = components['schemas']['TrainingSessionResponse'];

/** Regras de `RescheduleSessionCommandValidator`: futuro, duração > 0, local ≤ 255. */
const rescheduleSchema = z
  .object({
    date: z.string().min(1, 'Indica o dia.'),
    time: z.string().min(1, 'Indica a hora.'),
    duration_minutes: z
      .string()
      .trim()
      .refine(
        (value) => /^\d+$/.test(value) && Number(value) > 0,
        'A duração tem de ser um número de minutos maior que zero.'
      ),
    location: z.string().trim().max(255, 'O local não pode exceder 255 caracteres.'),
  })
  .refine(
    (values) => {
      const instant = localInstant(values.date, values.time);
      return instant === null || instant > new Date();
    },
    { path: ['time'], message: 'A sessão tem de começar no futuro.' }
  );
type RescheduleValues = z.input<typeof rescheduleSchema>;

const SERVER_FIELDS: Record<string, keyof RescheduleValues> = {
  StartsAt: 'time',
  DurationMinutes: 'duration_minutes',
  Location: 'location',
};

const CONFLICT_FIELDS: Record<string, keyof RescheduleValues> = {
  session_client_day_conflict: 'date',
  session_schedule_conflict: 'time',
};

/**
 * Formulário "Reagendar" de uma sessão agendada: dia, hora, duração e local.
 *
 * @param session Sessão a reagendar (só `scheduled` chega aqui).
 * @param onSaved Chamado depois de guardar.
 */
export function RescheduleForm({ session, onSaved }: { session: Session; onSaved: () => void }) {
  const mutation = useRescheduleSessionMutation();
  const start = splitStartsAt(session.starts_at);
  const form = useForm<RescheduleValues>({
    defaultValues: {
      date: start.date,
      time: start.time,
      duration_minutes: String(session.duration_minutes),
      location: session.location ?? '',
    },
  });
  const errors = form.formState.errors;

  async function submit(values: RescheduleValues) {
    const parsed = rescheduleSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof RescheduleValues, { message: issue.message });
      }
      return;
    }

    const data = parsed.data;
    try {
      await mutation.mutateAsync({
        sessionId: session.id,
        body: {
          starts_at: toStartsAt(data.date, data.time),
          duration_minutes: Number(data.duration_minutes),
          location: data.location || null,
        },
      });
      toast.success(`Sessão de ${session.client_name} reagendada.`);
      onSaved();
    } catch (error) {
      if (!isApiProblem(error)) {
        form.setError('root', { message: 'Não foi possível reagendar a sessão. Tenta novamente.' });
        return;
      }
      const conflictField = CONFLICT_FIELDS[error.code];
      if (conflictField !== undefined) {
        form.setError(conflictField, { message: SESSION_ERRORS[error.code] });
      } else if (error.hasFieldErrors) {
        for (const fieldError of error.fieldErrors) {
          const field = SERVER_FIELDS[fieldError.field];
          if (field !== undefined)
            form.setError(field, {
              message:
                fieldError.code === 'session_starts_at_not_future'
                  ? 'A sessão tem de começar no futuro.'
                  : fieldError.message,
            });
        }
      } else {
        form.setError('root', {
          message:
            SESSION_ERRORS[error.code] ?? 'Não foi possível reagendar a sessão. Tenta novamente.',
        });
      }
    }
  }

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="flex h-full flex-col gap-4 pb-4"
    >
      <div className="grid gap-4 sm:grid-cols-3">
        <FormField label="Dia" error={errors.date?.message}>
          {(control) => <Input {...control} type="date" {...form.register('date')} />}
        </FormField>
        <FormField label="Hora" error={errors.time?.message}>
          {(control) => <Input {...control} type="time" {...form.register('time')} />}
        </FormField>
        <FormField label="Duração (min)" error={errors.duration_minutes?.message}>
          {(control) => (
            <Input {...control} inputMode="numeric" {...form.register('duration_minutes')} />
          )}
        </FormField>
      </div>
      <FormField label="Local" error={errors.location?.message}>
        {(control) => <Input {...control} placeholder="Opcional" {...form.register('location')} />}
      </FormField>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Reagendar'}
        </Button>
      </div>
    </form>
  );
}
