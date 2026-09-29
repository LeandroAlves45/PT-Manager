import { useEffect, useRef } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { balanceLabel, useUsablePacksQuery } from '@/features/packs';
import { useCreateSessionMutation } from '@/features/sessions/api/mutations';
import { localInstant, toStartsAt, todayKey } from '@/features/sessions/lib/dates';
import { SESSION_ERRORS } from '@/features/sessions/lib/sessionStatus';
import { isApiProblem } from '@/shared/api/problem';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';
import { Textarea } from '@/shared/components/ui/textarea';
import { formatDate } from '@/shared/lib/format';

/** Valor do `<select>` de pack para "não descontar de nenhum pack". */
const NO_PACK = 'none';

/**
 * Regras de `CreateSessionCommandValidator`: cliente obrigatório, início no futuro,
 * duração inteira > 0 (sem máximo no backend), local ≤ 255, tipo ≤ 50.
 */
const sessionSchema = z
  .object({
    client: z.custom<ClientChoice | null>().refine((value) => value !== null, 'Escolhe o cliente.'),
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
    session_type: z.string().trim().max(50, 'O tipo não pode exceder 50 caracteres.'),
    notes: z.string(),
    pack: z.string(),
  })
  .refine(
    (values) => {
      const instant = localInstant(values.date, values.time);
      return instant === null || instant > new Date();
    },
    { path: ['time'], message: 'A sessão tem de começar no futuro.' }
  );
type SessionValues = z.input<typeof sessionSchema>;

/** Campos do backend para os campos do formulário. */
const SERVER_FIELDS: Record<string, keyof SessionValues> = {
  ClientId: 'client',
  StartsAt: 'time',
  DurationMinutes: 'duration_minutes',
  Location: 'location',
  SessionType: 'session_type',
  ClientSessionPackId: 'pack',
};

/** Conflitos 409 que pertencem a um campo concreto. */
const CONFLICT_FIELDS: Record<string, keyof SessionValues> = {
  session_client_day_conflict: 'date',
  session_schedule_conflict: 'time',
  session_pack_not_available: 'pack',
};

/**
 * Formulário "Marcar sessão".
 *
 * O pack pré-escolhido é o primeiro com saldo, pela ordem do servidor (o que acaba
 * primeiro). O personal trainer pode escolher outro ou "Sem pack". O saldo só desce
 * quando a sessão é concluída ou marcada como falta.
 *
 * @param client Cliente fixo (tab do detalhe) ou `null` para escolher na pesquisa.
 * @param defaultDate Dia pré-preenchido (o dia aberto na agenda).
 * @param onSaved Chamado depois de marcar.
 */
export function SessionForm({
  client,
  defaultDate,
  onSaved,
}: {
  client: ClientChoice | null;
  defaultDate?: string;
  onSaved: () => void;
}) {
  const mutation = useCreateSessionMutation();
  const form = useForm<SessionValues>({
    defaultValues: {
      client,
      date: defaultDate ?? todayKey(),
      time: '',
      duration_minutes: '60',
      location: '',
      session_type: '',
      notes: '',
      pack: '',
    },
  });
  const errors = form.formState.errors;
  const chosen = useWatch({ control: form.control, name: 'client' });
  const packs = useUsablePacksQuery(chosen?.id ?? null);

  // Pré-escolhe o primeiro pack com saldo (ou "Sem pack") **uma vez por cliente escolhido**.
  // Não pode correr a cada refetch de `usable` (foco da janela, `staleTime` de 30 s): isso
  // desfazia em silêncio a escolha manual do personal trainer.
  const suggestedFor = useRef<string | null>(null);
  useEffect(() => {
    if (chosen === null || packs.data === undefined || suggestedFor.current === chosen.id) return;
    suggestedFor.current = chosen.id;
    form.setValue('pack', packs.data[0]?.id ?? NO_PACK);
  }, [chosen, packs.data, form]);

  async function submit(values: SessionValues) {
    const parsed = sessionSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof SessionValues, { message: issue.message });
      }
      return;
    }

    const data = parsed.data;
    const chosen = data.client;
    if (chosen === null) return;

    try {
      await mutation.mutateAsync({
        client_id: chosen.id,
        client_session_pack_id: data.pack === NO_PACK || data.pack === '' ? null : data.pack,
        starts_at: toStartsAt(data.date, data.time),
        duration_minutes: Number(data.duration_minutes),
        location: data.location || null,
        session_type: data.session_type || null,
        notes: data.notes.trim() || null,
      });
      toast.success(`Sessão marcada com ${chosen.name}.`);
      onSaved();
    } catch (error) {
      if (!isApiProblem(error)) {
        form.setError('root', { message: 'Não foi possível marcar a sessão. Tenta novamente.' });
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
            SESSION_ERRORS[error.code] ?? 'Não foi possível marcar a sessão. Tenta novamente.',
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
      {client === null && (
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
      <FormField label="Pack" error={errors.pack?.message}>
        {(control) => (
          <NativeSelect
            {...control}
            disabled={chosen === null || packs.isPending}
            {...form.register('pack')}
          >
            {chosen === null ? (
              <option value="">Escolhe primeiro o cliente</option>
            ) : packs.isPending ? (
              <option value="">A carregar packs…</option>
            ) : (
              <>
                {packs.data?.map((pack) => (
                  <option key={pack.id} value={pack.id}>
                    {pack.pack_name} · {balanceLabel(pack)}
                    {pack.expected_end_date !== null &&
                      ` · fim ${formatDate(pack.expected_end_date)}`}
                  </option>
                ))}
                <option value={NO_PACK}>Sem pack</option>
              </>
            )}
          </NativeSelect>
        )}
      </FormField>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Local" error={errors.location?.message}>
          {(control) => (
            <Input {...control} placeholder="Opcional" {...form.register('location')} />
          )}
        </FormField>
        <FormField label="Tipo de sessão" error={errors.session_type?.message}>
          {(control) => (
            <Input
              {...control}
              placeholder="Opcional (ex.: avaliação)"
              {...form.register('session_type')}
            />
          )}
        </FormField>
      </div>
      <FormField label="Notas" error={errors.notes?.message}>
        {(control) => <Textarea {...control} rows={3} {...form.register('notes')} />}
      </FormField>
      <p className="text-muted-foreground text-sm">
        O tipo e as notas não se podem mudar depois; reagendar muda o dia, a hora, a duração e o
        local.
      </p>
      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}
      <div className="border-border mt-auto flex justify-end gap-2 border-t pt-4">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'A guardar…' : 'Marcar sessão'}
        </Button>
      </div>
    </form>
  );
}
