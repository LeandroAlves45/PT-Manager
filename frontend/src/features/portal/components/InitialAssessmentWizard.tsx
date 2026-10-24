import { useEffect, useId, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { useSubmitInitialAssessmentMutation } from '@/features/portal/api/portal';
import {
  ASSESSMENT_STEPS,
  assessmentIssues,
  assessmentSchema,
  BodyAndGoalsFields,
  emptyAssessmentValues,
  HabitsFields,
  MeasurementsFields,
  serverAssessmentIssues,
  toAssessmentRequest,
  type AssessmentIssue,
  type AssessmentValues,
} from '@/features/initial-assessment';
import { isApiProblem } from '@/shared/api/problem';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { Button } from '@/shared/components/ui/button';
import { Progress } from '@/shared/components/ui/progress';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';
import { formatNumber } from '@/shared/lib/format';

const LAST_STEP = ASSESSMENT_STEPS.length - 1;

/**
 * Tempo em que "Enviar avaliação" fica desativado ao chegar ao último passo.
 *
 * "Seguinte" e "Enviar avaliação" ocupam o mesmo sítio do ecrã. Sem esta espera, o segundo
 * toque de um toque duplo em "Seguinte" enviava a avaliação sem o cliente ver o último passo,
 * e o envio não se desfaz.
 */
const SEND_HOLD_MS = 500;

/** Como terminou o envio: entregue agora, ou já existia uma avaliação (409). */
export type WizardOutcome = 'sent' | 'exists';

/**
 * Preenchimento da avaliação inicial pelo cliente, em três passos: corpo e objetivos, medidas
 * e hábitos e saúde.
 *
 * É um só formulário: o passo é só o que está à vista e os valores dos outros ficam guardados
 * ao avançar e recuar. "Seguinte" valida o passo atual; "Enviar" valida tudo e volta ao
 * primeiro passo com erro. O que foi escrito vive só em memória (são dados de saúde), por isso
 * sair com dados escritos pede confirmação; depois de entregue o aviso deixa de valer.
 *
 * @param onFinished Chamado quando o envio terminou (entregue, ou já existia). O ecrã decide o
 *   que mostra a seguir.
 */
export function InitialAssessmentWizard({
  onFinished,
}: {
  onFinished: (outcome: WizardOutcome) => void;
}) {
  const submitMutation = useSubmitInitialAssessmentMutation();
  const form = useForm<AssessmentValues>({ defaultValues: emptyAssessmentValues() });
  const [step, setStep] = useState(0);
  const [outcome, setOutcome] = useState<WizardOutcome | null>(null);
  const [sendHeld, setSendHeld] = useState(false);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const opened = useRef(false);
  const holdId = useId();
  const errors = form.formState.errors;
  const blocker = useUnsavedChangesGuard(form.formState.isDirty && outcome === null);

  // O fim do envio chega ao ecrã no mesmo momento em que o guarda de saída se desliga (os dois
  // estados mudam no mesmo render). Não pode ser um efeito deste componente: o refetch do estado
  // (`pending: false`) pode trocar o formulário pela mensagem e desmontá-lo antes de o efeito
  // correr, e o ecrã nunca voltaria ao Início.
  function finish(value: WizardOutcome) {
    setOutcome(value);
    onFinished(value);
  }

  // Ao mudar de passo o foco vai para o título. No primeiro render não.
  useEffect(() => {
    if (opened.current) headingRef.current?.focus();
    opened.current = true;
  }, [step]);

  // Liberta "Enviar avaliação" depois da espera que trava o toque duplo.
  useEffect(() => {
    if (!sendHeld) return;

    const timer = setTimeout(() => setSendHeld(false), SEND_HOLD_MS);
    return () => clearTimeout(timer);
  }, [sendHeld]);

  /** Mostra os erros e leva o cliente ao primeiro passo que os tem. */
  function show(issues: readonly AssessmentIssue[]) {
    form.clearErrors();
    for (const issue of issues) form.setError(issue.field, { message: issue.message });
    setStep(Math.min(...issues.map((issue) => issue.step)));
  }

  async function submit(values: AssessmentValues) {
    const issues = assessmentIssues(values);

    if (step < LAST_STEP) {
      const current = issues.filter((issue) => issue.step === step);
      if (current.length > 0) show(current);
      else {
        form.clearErrors();
        setStep(step + 1);

        if (step + 1 === LAST_STEP) setSendHeld(true);
      }
      return;
    }

    const parsed = assessmentSchema.safeParse(values);
    if (!parsed.success) {
      show(issues);
      return;
    }

    form.clearErrors();

    try {
      await submitMutation.mutateAsync(toAssessmentRequest(parsed.data));
      toast.success('Avaliação inicial enviada.');
      finish('sent');
    } catch (failure) {
      if (isApiProblem(failure) && failure.code === 'initial_assessment_already_exists') {
        finish('exists');
        return;
      }

      const fromServer = isApiProblem(failure) ? serverAssessmentIssues(failure.fieldErrors) : [];
      if (fromServer.length > 0) {
        show(fromServer);
        return;
      }

      form.setError('root', {
        message: 'Não foi possível enviar a avaliação. O que escreveste continua aqui.',
      });
    }
  }

  const pending = submitMutation.isPending;

  return (
    <form
      noValidate
      aria-label="Avaliação inicial"
      onSubmit={form.handleSubmit((values) => void submit(values))}
      className="space-y-4"
    >
      <div className="space-y-2">
        <p className="text-muted-foreground font-mono text-xs tabular-nums">
          Passo {formatNumber(step + 1)} de {formatNumber(ASSESSMENT_STEPS.length)}
        </p>
        <Progress
          value={Math.round(((step + 1) / ASSESSMENT_STEPS.length) * 100)}
          aria-label="Progresso da avaliação inicial"
        />
        <h2 ref={headingRef} tabIndex={-1} className="font-display text-xl outline-none">
          {ASSESSMENT_STEPS[step]}
        </h2>
      </div>

      {step === 0 && (
        <div className="space-y-4">
          <BodyAndGoalsFields form={form} />
        </div>
      )}

      {step === 1 && (
        <div className="space-y-4">
          <p className="text-muted-foreground text-sm">
            Todas as medidas são opcionais. Preenche só as que tiraste.
          </p>
          <MeasurementsFields form={form} />
        </div>
      )}

      {step === 2 && (
        <div className="space-y-4">
          <p className="text-muted-foreground text-sm">
            Tudo opcional. Escreve o que achares útil para o teu personal trainer.
          </p>
          <HabitsFields form={form} />
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
        {/* Nós diferentes, com `key`: o botão de enviar nunca herda o toque do "Seguinte". */}
        {step < LAST_STEP ? (
          <Button key="next" type="submit" className="min-h-11 flex-1">
            Seguinte
          </Button>
        ) : (
          <Button
            key="send"
            type="submit"
            className="min-h-11 flex-1"
            disabled={pending || sendHeld}
            aria-describedby={sendHeld ? holdId : undefined}
          >
            {pending ? 'A enviar…' : 'Enviar avaliação'}
          </Button>
        )}
      </div>
      {step === LAST_STEP && sendHeld && (
        <p id={holdId} className="text-muted-foreground text-sm">
          Revê as respostas deste passo. Já podes enviar daqui a instantes.
        </p>
      )}

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem enviar?"
        description="O que escreveste nesta avaliação não fica guardado."
        confirmLabel="Sair sem enviar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </form>
  );
}
