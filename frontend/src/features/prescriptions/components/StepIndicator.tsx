import { cn } from '@/shared/lib/utils';

/**
 * Indicador dos passos de um editor de plano: «Passo 1 de 2 · Detalhes». Partilhado pelos planos
 * de treino e alimentares, para os dois terem o mesmo aspeto.
 *
 * Não é navegação: os botões Seguinte e Anterior validam e movem entre passos. As barras usam a
 * cor da marca (`primary`) e o passo atual fica por extenso e anunciado (`aria-live`), para não
 * depender só da cor.
 *
 * @param step Passo atual, a começar em 1.
 * @param steps Nome de cada passo, por ordem.
 */
export function StepIndicator({ step, steps }: { step: number; steps: readonly string[] }) {
  return (
    <div className="space-y-2">
      <p className="text-primary text-sm font-medium" aria-live="polite">
        Passo {step} de {steps.length} · {steps[step - 1]}
      </p>
      <div aria-hidden className="flex gap-2">
        {steps.map((label, index) => (
          <span
            key={label}
            className={cn(
              'h-1.5 flex-1 rounded-full',
              index < step ? 'bg-primary' : 'bg-primary/20'
            )}
          />
        ))}
      </div>
    </div>
  );
}
