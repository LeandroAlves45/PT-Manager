import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useBlocker } from 'react-router';
import { toast } from 'sonner';

import { FEEDBACK_LABELS } from '@/features/check-ins';
import { MEASUREMENT_LABELS } from '@/features/clients';
import { useRespondCheckInMutation } from '@/features/portal/api/portal';
import {
  CHECK_IN_STEPS,
  FEEDBACK_KEYS,
  MEASUREMENT_KEYS,
  checkInIssues,
  checkInStateMessage,
  emptyCheckInValues,
  serverCheckInIssues,
  toCheckInAnswer,
  type CheckInFormValues,
  type CheckInIssue,
} from '@/features/portal/lib/checkIn';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Progress } from '@/shared/components/ui/progress';
import { Textarea } from '@/shared/components/ui/textarea';
import { formatNumber } from '@/shared/lib/format';

const LAST_STEP = CHECK_IN_STEPS.length - 1;

/**
 * Resposta ao check-in de hoje, em três passos: peso, medidas e como correu.
 *
 * É um só formulário: o passo é só o que está à vista, e os valores dos outros passos ficam
 * guardados enquanto o cliente avança e recua. "Seguinte" valida o passo atual; "Enviar"
 * valida tudo e volta ao primeiro passo com erro. Só o peso é obrigatório.
 *
 * O que foi escrito vive só em memória (são dados de saúde, não vão para o armazenamento do
 * browser). Por isso, sair do ecrã com dados escritos pede confirmação.
 *
 * @param checkInId Check-in por responder (`GET /portal/my-check-ins/due`).
 * @param onSent Chamado depois de o servidor aceitar a resposta.
 */
export function CheckInForm({ checkInId, onSent }: { checkInId: string; onSent: () => void }) {
  const respond = useRespondCheckInMutation();
  const form = useForm<CheckInFormValues>({ defaultValues: emptyCheckInValues() });
  const [step, setStep] = useState(0);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const opened = useRef(false);
  const errors = form.formState.errors;
  const dirty = form.formState.isDirty;

  // Terminar a sessão não passa por aqui: o portal desmonta antes de a navegação acontecer.
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty && currentLocation.pathname !== nextLocation.pathname
  );

  // Fechar o separador ou recarregar: o browser mostra o seu próprio aviso.
  useEffect(() => {
    if (!dirty) return;

    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  // Ao mudar de passo o foco vai para o título, para o leitor de ecrã anunciar onde se está.
  // No primeiro render não: o foco fica onde o cliente o deixou ao abrir o ecrã.
  useEffect(() => {
    if (opened.current) headingRef.current?.focus();
    opened.current = true;
  }, [step]);

  /** Mostra os erros e leva o cliente ao primeiro passo que os tem. */
  function show(issues: readonly CheckInIssue[]) {
    form.clearErrors();
    for (const issue of issues) form.setError(issue.field, { message: issue.message });
    setStep(Math.min(...issues.map((issue) => issue.step)));
  }

  async function submit(values: CheckInFormValues) {
    const issues = checkInIssues(values);

    if (step < LAST_STEP) {
      const current = issues.filter((issue) => issue.step === step);
      if (current.length > 0) show(current);
      else {
        form.clearErrors();
        setStep(step + 1);
      }
      return;
    }

    if (issues.length > 0) {
      show(issues);
      return;
    }

    form.clearErrors();

    try {
      await respond.mutateAsync({ checkInId, answer: toCheckInAnswer(values) });
      // O ecrã troca o formulário pela confirmação: o aviso de saída sai com ele.
      onSent();
    } catch (failure) {
      const stateMessage = checkInStateMessage(failure);
      if (stateMessage !== null) {
        toast.error(stateMessage);
        return;
      }

      const fromServer = isApiProblem(failure) ? serverCheckInIssues(failure.fieldErrors) : [];
      if (fromServer.length > 0) {
        show(fromServer);
        return;
      }

      form.setError('root', {
        message: 'Não foi possível enviar o check-in. O que escreveste continua aqui.',
      });
    }
  }

  const pending = respond.isPending;

  return (
    <form
      noValidate
      aria-label="Responder ao check-in"
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="space-y-4"
    >
      <div className="space-y-2">
        <p className="text-muted-foreground font-mono text-xs tabular-nums">
          Passo {formatNumber(step + 1)} de {formatNumber(CHECK_IN_STEPS.length)}
        </p>
        <Progress
          value={Math.round(((step + 1) / CHECK_IN_STEPS.length) * 100)}
          aria-label="Progresso do check-in"
        />
        <h2 ref={headingRef} tabIndex={-1} className="font-display text-xl outline-none">
          {CHECK_IN_STEPS[step]}
        </h2>
      </div>

      {step === 0 && (
        <div className="space-y-4">
          <FormField label="Peso (kg)" error={errors.weight_kg?.message}>
            {(control) => (
              <Input
                {...control}
                inputMode="decimal"
                autoComplete="off"
                className="h-11 tabular-nums"
                {...form.register('weight_kg')}
              />
            )}
          </FormField>
          <FormField label="Massa gorda (%) (opcional)" error={errors.body_fat_percentage?.message}>
            {(control) => (
              <Input
                {...control}
                inputMode="decimal"
                autoComplete="off"
                className="h-11 tabular-nums"
                {...form.register('body_fat_percentage')}
              />
            )}
          </FormField>
        </div>
      )}

      {step === 1 && (
        <div className="space-y-4">
          <p className="text-muted-foreground text-sm">
            Todas as medidas são opcionais. Preenche só as que tiraste.
          </p>
          <div className="grid grid-cols-2 gap-4">
            {MEASUREMENT_KEYS.map((key) => (
              <FormField
                key={key}
                label={MEASUREMENT_LABELS[key]}
                error={errors.measurements?.[key]?.message}
              >
                {(control) => (
                  <Input
                    {...control}
                    inputMode="decimal"
                    autoComplete="off"
                    className="h-11 tabular-nums"
                    {...form.register(`measurements.${key}`)}
                  />
                )}
              </FormField>
            ))}
          </div>
        </div>
      )}

      {step === 2 && (
        <div className="space-y-4">
          <p className="text-muted-foreground text-sm">
            Tudo opcional. Escreve o que achares útil para o teu personal trainer.
          </p>
          {FEEDBACK_KEYS.map((key) => (
            <FormField
              key={key}
              label={FEEDBACK_LABELS[key]}
              error={errors.feedback?.[key]?.message}
            >
              {(control) => (
                <Textarea {...control} rows={2} {...form.register(`feedback.${key}`)} />
              )}
            </FormField>
          ))}
          <div className="grid grid-cols-2 gap-4">
            <FormField
              label="Adesão ao treino (%)"
              error={errors.training_adherence_score?.message}
            >
              {(control) => (
                <Input
                  {...control}
                  inputMode="numeric"
                  autoComplete="off"
                  className="h-11 tabular-nums"
                  {...form.register('training_adherence_score')}
                />
              )}
            </FormField>
            <FormField
              label="Adesão à alimentação (%)"
              error={errors.nutrition_adherence_score?.message}
            >
              {(control) => (
                <Input
                  {...control}
                  inputMode="numeric"
                  autoComplete="off"
                  className="h-11 tabular-nums"
                  {...form.register('nutrition_adherence_score')}
                />
              )}
            </FormField>
          </div>
          <FormField label="Notas" error={errors.notes?.message}>
            {(control) => <Textarea {...control} rows={3} {...form.register('notes')} />}
          </FormField>
        </div>
      )}

      {errors.root && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.message}
        </p>
      )}

      <div className="flex gap-2">
        {step > 0 && (
          <Button
            type="button"
            variant="outline"
            className="min-h-11 flex-1"
            disabled={pending}
            onClick={() => setStep(step - 1)}
          >
            Anterior
          </Button>
        )}
        <Button type="submit" className="min-h-11 flex-1" disabled={pending}>
          {step < LAST_STEP ? 'Seguinte' : pending ? 'A enviar…' : 'Enviar check-in'}
        </Button>
      </div>

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem enviar?"
        description="O que escreveste neste check-in não fica guardado."
        confirmLabel="Sair sem enviar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </form>
  );
}
