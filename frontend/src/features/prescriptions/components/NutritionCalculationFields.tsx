import { FormField } from '@/shared/components/FormField';
import { Input } from '@/shared/components/ui/input';
import type { Calculation } from '@/features/prescriptions/lib/nutrition';

function toNumber(value: string): number | null {
  return value.trim() === '' ? null : Number(value);
}

/** Campos do cálculo; cada mudança invalida o preview que o personal trainer viu. */
export function NutritionCalculationFields({
  value,
  onChange,
  disabled,
}: {
  value: Calculation;
  onChange: (next: Calculation) => void;
  disabled: boolean;
}) {
  function change<K extends keyof Calculation>(key: K, next: Calculation[K]) {
    onChange({ ...value, [key]: next });
  }

  return (
    <section className="space-y-4">
      <h3 className="font-display text-xl">Cálculo nutricional</h3>
      <div className="grid gap-3 sm:grid-cols-3">
        <FormField label="Origem da energia">
          {(control) => (
            <select
              {...control}
              className="border-input bg-background h-9 rounded-md border px-3"
              value={value.calculation_origin}
              disabled={disabled}
              onChange={(event) => change('calculation_origin', event.target.value)}
            >
              <option value="">Escolhe</option>
              <option value="formula">Fórmula</option>
              <option value="manual_energy">Energia manual</option>
            </select>
          )}
        </FormField>
        <FormField label="Peso usado (kg)">
          {(control) => (
            <Input
              {...control}
              type="number"
              min={0.01}
              step={0.01}
              value={value.weight_kg || ''}
              disabled={disabled}
              onChange={(event) => change('weight_kg', Number(event.target.value))}
            />
          )}
        </FormField>
        {value.calculation_origin === 'manual_energy' && (
          <FormField label="Energia alvo (kcal)">
            {(control) => (
              <Input
                {...control}
                type="number"
                min={1}
                step={0.01}
                value={value.manual_target_kcal ?? ''}
                disabled={disabled}
                onChange={(event) => change('manual_target_kcal', toNumber(event.target.value))}
              />
            )}
          </FormField>
        )}
        {value.calculation_origin === 'formula' && (
          <>
            <FormField label="Fórmula">
              {(control) => (
                <select
                  {...control}
                  className="border-input bg-background h-9 rounded-md border px-3"
                  value={value.energy_formula ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('energy_formula', event.target.value || null)}
                >
                  <option value="">Escolhe</option>
                  <option value="harris_benedict">Harris Benedict</option>
                  <option value="mifflin_st_jeor">Mifflin St Jeor</option>
                  <option value="cunningham">Cunningham</option>
                  <option value="tinsley">Tinsley</option>
                </select>
              )}
            </FormField>
            <FormField label="Altura (cm)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0.01}
                  step={0.01}
                  value={value.height_cm ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('height_cm', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Idade">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={18}
                  max={120}
                  value={value.age ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('age', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Sexo biológico">
              {(control) => (
                <select
                  {...control}
                  className="border-input bg-background h-9 rounded-md border px-3"
                  value={value.sex ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('sex', event.target.value || null)}
                >
                  <option value="">Escolhe</option>
                  <option value="male">Masculino</option>
                  <option value="female">Feminino</option>
                </select>
              )}
            </FormField>
            <FormField label="Gordura corporal (%)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={value.body_fat_percentage ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('body_fat_percentage', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Atividade">
              {(control) => (
                <select
                  {...control}
                  className="border-input bg-background h-9 rounded-md border px-3"
                  value={value.activity_level ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('activity_level', event.target.value || null)}
                >
                  <option value="">Escolhe</option>
                  <option value="sedentary">Sedentário</option>
                  <option value="lightly_active">Ligeiramente ativo</option>
                  <option value="moderately_active">Moderadamente ativo</option>
                  <option value="very_active">Muito ativo</option>
                  <option value="extremely_active">Extremamente ativo</option>
                </select>
              )}
            </FormField>
            <FormField label="Objetivo">
              {(control) => (
                <select
                  {...control}
                  className="border-input bg-background h-9 rounded-md border px-3"
                  value={value.goal_type ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('goal_type', event.target.value || null)}
                >
                  <option value="">Escolhe</option>
                  <option value="maintenance">Manutenção</option>
                  <option value="deficit">Défice</option>
                  <option value="surplus">Excedente</option>
                </select>
              )}
            </FormField>
            <FormField label="Ajuste (kcal)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  step={0.01}
                  value={value.goal_adjustment_kcal ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('goal_adjustment_kcal', toNumber(event.target.value))}
                />
              )}
            </FormField>
          </>
        )}
        <FormField label="Modo dos macros">
          {(control) => (
            <select
              {...control}
              className="border-input bg-background h-9 rounded-md border px-3"
              value={value.macro_mode}
              disabled={disabled}
              onChange={(event) => change('macro_mode', event.target.value)}
            >
              <option value="">Escolhe</option>
              <option value="percentage">Percentagem</option>
              <option value="grams_per_kg">Gramas por kg</option>
              <option value="manual_grams">Gramas manuais</option>
            </select>
          )}
        </FormField>
        {value.macro_mode === 'percentage' && (
          <>
            <FormField label="Proteína (%)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={value.protein_percentage ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('protein_percentage', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Hidratos (%)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={value.carbs_percentage ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('carbs_percentage', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Gordura (%)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={value.fats_percentage ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('fats_percentage', toNumber(event.target.value))}
                />
              )}
            </FormField>
          </>
        )}
        {value.macro_mode === 'grams_per_kg' && (
          <>
            <FormField label="Proteína (g/kg)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0.01}
                  step={0.01}
                  value={value.protein_grams_per_kg ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('protein_grams_per_kg', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Gordura (g/kg)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0.01}
                  step={0.01}
                  value={value.fats_grams_per_kg ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('fats_grams_per_kg', toNumber(event.target.value))}
                />
              )}
            </FormField>
          </>
        )}
        {value.macro_mode === 'manual_grams' && (
          <>
            <FormField label="Proteína (g)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  step={0.01}
                  value={value.protein_grams ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('protein_grams', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Hidratos (g)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  step={0.01}
                  value={value.carbs_grams ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('carbs_grams', toNumber(event.target.value))}
                />
              )}
            </FormField>
            <FormField label="Gordura (g)">
              {(control) => (
                <Input
                  {...control}
                  type="number"
                  min={0}
                  step={0.01}
                  value={value.fats_grams ?? ''}
                  disabled={disabled}
                  onChange={(event) => change('fats_grams', toNumber(event.target.value))}
                />
              )}
            </FormField>
          </>
        )}
      </div>
    </section>
  );
}
