import { remaining, type Targets, type Totals } from '@/features/prescriptions/lib/nutrition';
import { cn } from '@/shared/lib/utils';

interface Metric {
  readonly key: 'kcal' | 'protein' | 'carbs' | 'fats';
  readonly label: string;
  readonly unit: string;
  readonly text: string;
  readonly bar: string;
}

/**
 * Energia em tom neutro (texto do tema) e macros nos tokens fixos (AA nos dois temas): nenhuma
 * métrica usa a cor da marca, para a energia não se confundir com os hidratos quando a marca é
 * laranja ou âmbar.
 */
const METRICS: readonly Metric[] = [
  { key: 'kcal', label: 'Energia', unit: 'kcal', text: 'text-foreground', bar: 'bg-foreground' },
  {
    key: 'protein',
    label: 'Proteína',
    unit: 'g',
    text: 'text-macro-protein',
    bar: 'bg-macro-protein',
  },
  { key: 'carbs', label: 'Hidratos', unit: 'g', text: 'text-macro-carbs', bar: 'bg-macro-carbs' },
  { key: 'fats', label: 'Gordura', unit: 'g', text: 'text-macro-fats', bar: 'bg-macro-fats' },
];

/** Número em PT-PT com no máximo `digits` casas (sem zeros à direita). */
function formatAmount(value: number, digits: number): string {
  return new Intl.NumberFormat('pt-PT', { maximumFractionDigits: digits }).format(value);
}

/**
 * Arredonda à precisão que se mostra antes de decidir o sinal: −0,4 kcal com 0 casas é «0» e
 * «meta atingida», não «−0» nem «excede 0 kcal». O `|| 0` troca −0 por 0.
 */
function toShown(value: number, digits: number): number {
  const scale = 10 ** digits;
  return Math.round(value * scale) / scale || 0;
}

/** Valor com sinal: o que excede leva «−» (U+2212), nunca um hífen. */
function formatSigned(value: number, digits: number): string {
  const shown = toShown(value, digits);
  return `${shown < 0 ? '−' : ''}${formatAmount(Math.abs(shown), digits)}`;
}

/** Frase para leitores de ecrã («excede 200 kcal», «faltam 120 g», «meta atingida»). */
function sentence(value: number, unit: string, digits: number): string {
  const shown = toShown(value, digits);
  if (shown < 0) return `excede ${formatAmount(-shown, digits)} ${unit}`;
  if (shown === 0) return 'meta atingida';
  return `faltam ${formatAmount(shown, digits)} ${unit}`;
}

/**
 * Meta, soma dos alimentos e o que falta, para energia, proteína, hidratos e gordura. O que
 * falta desce a cada alimento e pode ficar negativo («−120 kcal», «excede 120 kcal»): é só
 * informação, nunca bloqueia nem recusa um alimento. A fibra não tem meta no cálculo, por
 * isso mostra só o total.
 *
 * @param targets Metas do cálculo (preview ou snapshot).
 * @param totals Soma dos alimentos do plano (ou da refeição).
 * @param sticky Fica fixo por baixo da barra de topo ao deslizar a página (passo das refeições).
 */
export function NutritionBalance({
  targets,
  totals,
  sticky = false,
}: {
  targets: Targets;
  totals: Totals;
  sticky?: boolean;
}) {
  return (
    <section
      aria-label="O que falta para a meta"
      data-testid="nutrition-balance"
      className={cn(
        'border-border bg-card space-y-2 rounded-xl border p-3 shadow-xs',
        sticky && 'sticky top-14 z-[5]'
      )}
    >
      <div className="flex items-baseline justify-between gap-3">
        <p className="text-muted-foreground text-xs">O que falta para a meta</p>
        <p className="text-macro-fiber text-xs font-medium tabular-nums">
          Fibra {formatAmount(totals.fiber, 1)} g
        </p>
      </div>
      <ul className="grid grid-cols-4 gap-2">
        {METRICS.map((metric) => {
          const digits = metric.key === 'kcal' ? 0 : 1;
          const target = targets[metric.key];
          const total = totals[metric.key];
          const left = remaining(target, total);
          const percent = target > 0 ? Math.min(100, Math.round((total / target) * 100)) : 0;
          return (
            <li key={metric.key} className="min-w-0 space-y-0.5">
              <p className={cn('truncate text-xs font-medium', metric.text)}>{metric.label}</p>
              <p className="text-sm leading-tight font-semibold tabular-nums sm:text-lg">
                <span className={cn(left < 0 && 'font-bold')}>{formatSigned(left, digits)}</span>
                <span className="text-muted-foreground ml-0.5 text-xs font-normal">
                  {metric.unit}
                </span>
              </p>
              <span className="sr-only">{sentence(left, metric.unit, digits)}</span>
              <p aria-hidden className="text-muted-foreground truncate text-xs tabular-nums">
                {formatAmount(total, digits)} / {formatAmount(target, digits)}
              </p>
              <div
                role="progressbar"
                aria-label={`${metric.label} da meta`}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={percent}
                className="bg-muted h-1.5 overflow-hidden rounded-full"
              >
                <div
                  className={cn('h-full rounded-full', metric.bar)}
                  style={{ width: `${percent}%` }}
                />
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/**
 * Linha compacta com os totais de uma refeição: energia a azul, macros nas suas cores.
 */
export function MealTotalsLine({ totals }: { totals: Totals }) {
  return (
    <p className="flex flex-wrap gap-x-3 gap-y-0.5 text-xs font-medium tabular-nums">
      <span className="text-foreground font-medium">{formatAmount(totals.kcal, 0)} kcal</span>
      <span className="text-macro-protein">P {formatAmount(totals.protein, 1)} g</span>
      <span className="text-macro-carbs">H {formatAmount(totals.carbs, 1)} g</span>
      <span className="text-macro-fats">G {formatAmount(totals.fats, 1)} g</span>
      <span className="text-macro-fiber">F {formatAmount(totals.fiber, 1)} g</span>
    </p>
  );
}

/**
 * Resumo das metas de um cálculo: energia na cor da marca e macros nas suas cores.
 *
 * @param targets Metas do preview ou do snapshot do plano.
 */
export function TargetsSummary({ targets }: { targets: Targets }) {
  return (
    <div
      data-testid="targets-summary"
      className="border-border bg-card space-y-2 rounded-xl border p-4"
    >
      <p className="text-muted-foreground text-xs">Metas diárias</p>
      <p className="text-foreground text-2xl font-semibold tabular-nums">
        {formatAmount(targets.kcal, 0)} kcal
      </p>
      <p className="flex flex-wrap gap-x-4 gap-y-1 text-sm font-medium tabular-nums">
        <span className="text-macro-protein">Proteína {formatAmount(targets.protein, 1)} g</span>
        <span className="text-macro-carbs">Hidratos {formatAmount(targets.carbs, 1)} g</span>
        <span className="text-macro-fats">Gordura {formatAmount(targets.fats, 1)} g</span>
      </p>
    </div>
  );
}
