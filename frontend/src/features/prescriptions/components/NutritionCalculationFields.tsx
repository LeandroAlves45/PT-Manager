import { ClipboardCheck } from 'lucide-react';

import { FORMULA_MIN_AGE, type Calculation } from '@/features/prescriptions/lib/nutrition';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { NativeSelect } from '@/shared/components/ui/native-select';

function toNumber(value: string): number | null {
  return value.trim() === '' ? null : Number(value);
}

/** Campo numérico opcional do cálculo (vazio = `null`). */
function NumberField({
  label,
  value,
  onChange,
  disabled,
  min,
  max,
  step = 0.01,
}: {
  label: string;
  value: number | null;
  onChange: (next: number | null) => void;
  disabled: boolean;
  min?: number;
  max?: number;
  step?: number;
}) {
  return (
    <FormField label={label}>
      {(control) => (
        <Input
          {...control}
          type="number"
          inputMode="decimal"
          min={min}
          max={max}
          step={step}
          value={value ?? ''}
          disabled={disabled}
          onChange={(event) => onChange(toNumber(event.target.value))}
        />
      )}
    </FormField>
  );
}

/** Lista de opções com uma primeira linha vazia e desativada: nada vem escolhido por omissão. */
function ChoiceField({
  label,
  value,
  onChange,
  disabled,
  options,
}: {
  label: string;
  value: string | null;
  onChange: (next: string) => void;
  disabled: boolean;
  options: ReadonlyArray<readonly [value: string, label: string]>;
}) {
  return (
    <FormField label={label}>
      {(control) => (
        <NativeSelect
          {...control}
          value={value ?? ''}
          disabled={disabled}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="" disabled>
            Escolhe…
          </option>
          {options.map(([optionValue, optionLabel]) => (
            <option key={optionValue} value={optionValue}>
              {optionLabel}
            </option>
          ))}
        </NativeSelect>
      )}
    </FormField>
  );
}

/**
 * Campos do cálculo de kcal e macros: origem da energia (fórmula ou kcal à mão), objetivo e
 * ajuste, e a divisão das macros (percentagem, g/kg ou gramas à mão). Só mostra os campos
 * que o modo usa; `normalizeCalculation` anula os restantes antes de enviar. Cada mudança
 * passa por `onChange` e o editor anula o preview que o personal trainer viu.
 *
 * Nada vem pré-preenchido: «Usar avaliação inicial» copia a ficha e a avaliação do cliente
 * só quando o personal trainer clica. Abaixo dos 18 anos só existe «kcal à mão», porque o
 * servidor recusa fórmulas a menores.
 *
 * @param clientAge Idade do cliente; `null` quando ainda não se conhece.
 * @param canUseAssessment Mostra o botão «Usar avaliação inicial».
 * @param onUseAssessment Copia os valores do cliente para o cálculo.
 */
export function NutritionCalculationFields({
  value,
  onChange,
  disabled,
  clientAge = null,
  canUseAssessment = false,
  onUseAssessment,
}: {
  value: Calculation;
  onChange: (next: Calculation) => void;
  disabled: boolean;
  clientAge?: number | null;
  canUseAssessment?: boolean;
  onUseAssessment?: () => void;
}) {
  const formulaAllowed = clientAge === null || clientAge >= FORMULA_MIN_AGE;

  function change<K extends keyof Calculation>(key: K, next: Calculation[K]) {
    onChange({ ...value, [key]: next });
  }

  const originOptions: ReadonlyArray<readonly [string, string]> = formulaAllowed
    ? [
        ['formula', 'Fórmula'],
        ['manual_energy', 'Kcal à mão'],
      ]
    : [['manual_energy', 'Kcal à mão']];

  return (
    <section className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <h2 className="font-display text-xl">Kcal e macros</h2>
          {!formulaAllowed && (
            <p className="text-muted-foreground text-sm">
              As fórmulas são para {FORMULA_MIN_AGE} anos ou mais. Escreve as kcal à mão.
            </p>
          )}
        </div>
        {canUseAssessment && !disabled && onUseAssessment !== undefined && (
          <Button
            type="button"
            variant="outline"
            className="min-h-11 md:min-h-8"
            onClick={onUseAssessment}
          >
            <ClipboardCheck aria-hidden /> Usar avaliação inicial
          </Button>
        )}
      </div>

      <div className="space-y-3">
        <h3 className="text-sm font-medium">Energia</h3>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <ChoiceField
            label="Origem da energia"
            value={value.calculation_origin}
            disabled={disabled}
            options={originOptions}
            onChange={(next) => change('calculation_origin', next)}
          />
          <NumberField
            label="Peso usado (kg)"
            value={value.weight_kg || null}
            disabled={disabled}
            min={0.01}
            onChange={(next) => change('weight_kg', next ?? 0)}
          />
          {value.calculation_origin === 'manual_energy' && (
            <NumberField
              label="Energia alvo (kcal)"
              value={value.manual_target_kcal}
              disabled={disabled}
              min={1}
              onChange={(next) => change('manual_target_kcal', next)}
            />
          )}
          {value.calculation_origin === 'formula' && (
            <>
              <ChoiceField
                label="Fórmula"
                value={value.energy_formula}
                disabled={disabled}
                options={[
                  ['harris_benedict', 'Harris-Benedict'],
                  ['mifflin_st_jeor', 'Mifflin-St Jeor'],
                  ['cunningham', 'Cunningham'],
                  ['tinsley', 'Tinsley'],
                ]}
                onChange={(next) => change('energy_formula', next)}
              />
              <NumberField
                label="Altura (cm)"
                value={value.height_cm}
                disabled={disabled}
                min={0.01}
                onChange={(next) => change('height_cm', next)}
              />
              <NumberField
                label="Idade"
                value={value.age}
                disabled={disabled}
                min={FORMULA_MIN_AGE}
                max={120}
                step={1}
                onChange={(next) => change('age', next)}
              />
              <ChoiceField
                label="Sexo biológico"
                value={value.sex}
                disabled={disabled}
                options={[
                  ['male', 'Masculino'],
                  ['female', 'Feminino'],
                ]}
                onChange={(next) => change('sex', next)}
              />
              <NumberField
                label="Gordura corporal (%)"
                value={value.body_fat_percentage}
                disabled={disabled}
                min={0}
                max={100}
                onChange={(next) => change('body_fat_percentage', next)}
              />
              <ChoiceField
                label="Atividade"
                value={value.activity_level}
                disabled={disabled}
                options={[
                  ['sedentary', 'Sedentário'],
                  ['lightly_active', 'Ligeiramente ativo'],
                  ['moderately_active', 'Moderadamente ativo'],
                  ['very_active', 'Muito ativo'],
                  ['extremely_active', 'Extremamente ativo'],
                ]}
                onChange={(next) => change('activity_level', next)}
              />
              <ChoiceField
                label="Objetivo"
                value={value.goal_type}
                disabled={disabled}
                options={[
                  ['maintenance', 'Manutenção'],
                  ['deficit', 'Défice'],
                  ['surplus', 'Excedente'],
                ]}
                onChange={(next) => change('goal_type', next)}
              />
              <NumberField
                label="Ajuste (kcal)"
                value={value.goal_adjustment_kcal}
                disabled={disabled}
                min={0}
                onChange={(next) => change('goal_adjustment_kcal', next)}
              />
            </>
          )}
        </div>
      </div>

      <div className="space-y-3">
        <h3 className="text-sm font-medium">Macros</h3>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <ChoiceField
            label="Modo dos macros"
            value={value.macro_mode}
            disabled={disabled}
            options={[
              ['percentage', 'Percentagem'],
              ['grams_per_kg', 'Gramas por kg'],
              ['manual_grams', 'Gramas à mão'],
            ]}
            onChange={(next) => change('macro_mode', next)}
          />
          {value.macro_mode === 'percentage' && (
            <>
              <NumberField
                label="Proteína (%)"
                value={value.protein_percentage}
                disabled={disabled}
                min={0}
                max={100}
                onChange={(next) => change('protein_percentage', next)}
              />
              <NumberField
                label="Hidratos (%)"
                value={value.carbs_percentage}
                disabled={disabled}
                min={0}
                max={100}
                onChange={(next) => change('carbs_percentage', next)}
              />
              <NumberField
                label="Gordura (%)"
                value={value.fats_percentage}
                disabled={disabled}
                min={0}
                max={100}
                onChange={(next) => change('fats_percentage', next)}
              />
            </>
          )}
          {value.macro_mode === 'grams_per_kg' && (
            <>
              <NumberField
                label="Proteína (g/kg)"
                value={value.protein_grams_per_kg}
                disabled={disabled}
                min={0.01}
                onChange={(next) => change('protein_grams_per_kg', next)}
              />
              <NumberField
                label="Gordura (g/kg)"
                value={value.fats_grams_per_kg}
                disabled={disabled}
                min={0.01}
                onChange={(next) => change('fats_grams_per_kg', next)}
              />
            </>
          )}
          {value.macro_mode === 'manual_grams' && (
            <>
              <NumberField
                label="Proteína (g)"
                value={value.protein_grams}
                disabled={disabled}
                min={0}
                onChange={(next) => change('protein_grams', next)}
              />
              <NumberField
                label="Hidratos (g)"
                value={value.carbs_grams}
                disabled={disabled}
                min={0}
                onChange={(next) => change('carbs_grams', next)}
              />
              <NumberField
                label="Gordura (g)"
                value={value.fats_grams}
                disabled={disabled}
                min={0}
                onChange={(next) => change('fats_grams', next)}
              />
            </>
          )}
        </div>
      </div>
    </section>
  );
}
