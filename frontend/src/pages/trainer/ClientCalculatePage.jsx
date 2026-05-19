/**
 * ClientCalculatorPage.jsx — Calculadora de kcal/macros por cliente (NR-02)
 *
 * Rota: /trainer/clientes/:id/calculadora
 *
 * Fluxo em 3 passos:
 *   1 — Dados biométricos (pré-preenchidos da avaliação) → TMB/TDEE das 3 fórmulas
 *   2 — Selecção do TDEE + ajuste manual de kcal (défice/superavit)
 *   3 — Distribuição de macros (% ou g/kg)
 *
 * O backend é puro Python — sem BD, sem side effects.
 * Pode ser chamado múltiplas vezes com diferentes valores sem risco.
 */

import { useState, useEffect } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';

import { getClients } from '@/api/clientsApi';
import { getAssessmentsByClient } from '@/api/assessmentsApi';
import { calculateMacros, getActivityFactors } from '@/api/nutritionApi';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';

import {
  ArrowLeft,
  Calculator,
  ChevronRight,
  CheckCircle2,
  RefreshCw,
} from 'lucide-react';

// Constante: Percentagens sugeridas por objetivo

// valores de referência para distribuição de macros por objetivo
const MACRO_PRESETS = [
  { label: 'Perda de peso', protein_pct: 35, carbs_pct: 35, fats_pct: 30 },
  { label: 'Manutenção', protein_pct: 30, carbs_pct: 40, fats_pct: 30 },
  { label: 'Ganho muscular', protein_pct: 30, carbs_pct: 45, fats_pct: 25 },
  { label: 'Desempenho', protein_pct: 25, carbs_pct: 50, fats_pct: 25 },
];

// ============================================================
// Sub-componente: card de resultado por macronutriente
// ============================================================

// Componentes: barra visual da macro (ex: 150g de proteína)
/**
 * MacroResultCard — exibe um macronutriente com gramas, g/kg e % kcal.
 * Cor codificada: HC=laranja , P=azul, G=amarelo.
 */

function MacroResultCard({ label, grams, gPerKg, pct, color }) {
  const colors = {
    orange:
      'bg-orange-500/10 text-orange-600 dark:text-orange-400 border-orange-500/20',
    blue: 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20',
    yellow:
      'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 border-yellow-500/20',
  };

  return (
    <div className={`rounded-lg border p-4 space-y-1 ${colors[color]}`}>
      <p className="text-xs font-medium uppercase tracking-wide opacity-70">
        {label}
      </p>
      <p className="text-2xl font-bold">{grams}g</p>
      <div className="flex gap-3 text-xs opacity-80">
        <span>{gPerKg} g/kg</span>
        <span>·</span>
        <span>{pct}%</span>
      </div>
    </div>
  );
}

// Componente principal

export default function ClientCalculatePage() {
  const { id } = useParams(); // UUID do cliente vindo da URL
  const navigate = useNavigate();
  const location = useLocation();
  const cameFromMealPlans = location.state?.fromMealPlans === true;

  // Estado do cliente
  const [client, setClient] = useState(null);
  const [loadingClient, setLoadingClient] = useState(true);

  // Estado dos fatores de atividade (para o select)
  const [activityFactors, setActivityFactors] = useState([]);

  // Estado do fluxo de cálculo
  const [step, setStep] = useState(1); // 1, 2 ou 3

  // Resultado completo devolvido pelo backend
  const [calcResult, setCalcResult] = useState(null);

  // TDEE selecionado pelo Personal Trainer
  const [selectedTdee, setSelectedTdee] = useState(null);
  const [selectedFormulaLabel, setSelectedFormulaLabel] = useState('');
  const [kcalAdjust, setKcalAdjust] = useState(0); // ajuste manual de kcal (défice/superavit). défice (negativo) superavit (positivo)

  // kcal final = TDEE + ajuste
  const adjustedKcal = selectedTdee
    ? Math.max(0, Math.round(selectedTdee + kcalAdjust))
    : null;

  // Método de distribuição de macros
  const [macroMethod, setMacroMethod] = useState('percentages'); // 'percentages' ou 'grams_per_kg'

  // Resultado da distribuição de macros
  const [macroDistribution, setMacroDistribution] = useState(null);
  const [calculatingMacros, setCalculatingMacros] = useState(false);

  // Formulário de dados biométricos
  const {
    register: regBio,
    handleSubmit: handleBioSubmit,
    formState: { errors: bioErrors, isSubmitting: bioSubmitting },
    setValue: setBioValue,
    watch: watchBio,
  } = useForm({
    defaultValues: {
      weight_kg: '',
      height_cm: '',
      age: '',
      sex: 'male',
      activity_factor: 'moderately_active',
    },
  });

  // Formulário de distribuição
  const {
    register: regMacro,
    handleSubmit: handleMacroSubmit,
    formState: { errors: macroErrors },
    watch: watchMacro,
    reset: resetMacroForm,
  } = useForm({
    defaultValues: {
      protein_pct: 30,
      carbs_pct: 40,
      fats_pct: 30, // para método 'percentages'
      protein_g_per_kg: 2.0,
      carbs_g_per_kg: 3.0,
      fats_g_per_kg: 1.0, // para método 'grams_per_kg'
    },
  });

  // Soma das percentagens (para validação, alerta se não for 100%)
  const pctValues = watchMacro(['protein_pct', 'carbs_pct', 'fats_pct']);
  const pctSum = pctValues.reduce(
    (sum, val) => sum + (parseFloat(val) || 0),
    0
  );

  // Gramas calculadas em tempo real para método g/kg
  const weightKg =
    parseFloat(watchBio('weight_kg')) || (calcResult?.weightKg ?? 0);
  const gPerKgValues = watchMacro([
    'protein_g_per_kg',
    'carbs_g_per_kg',
    'fats_g_per_kg',
  ]);
  const gramsFromRatio = {
    protein: Math.round((parseFloat(gPerKgValues[0]) || 0) * weightKg),
    carbs: Math.round((parseFloat(gPerKgValues[1]) || 0) * weightKg),
    fats: Math.round((parseFloat(gPerKgValues[2]) || 0) * weightKg),
  };
  const kcalFromRatio =
    gramsFromRatio.protein * 4 +
    gramsFromRatio.carbs * 4 +
    gramsFromRatio.fats * 9;

  // Carregamento inicial
  // Carrega cliente, última avaliação e factores de actividade em paralelo.
  // A avaliação física (InitialAssessment) contém peso e altura reais — mais
  // actuais do que os campos do perfil do cliente que podem estar desactualizados.
  useEffect(() => {
    const init = async () => {
      try {
        // Carrega cliente e fatores de atividade em paralelo
        const [clientRes, factorsRes, assessmentRes] = await Promise.allSettled(
          [
            getClients({ Client_id: id }),
            getActivityFactors(),
            getAssessmentsByClient(id),
          ]
        );

        if (clientRes.status === 'fulfilled' && clientRes.value.length > 0) {
          const c = clientRes.value[0];
          setClient(c);
          // Pré-preenche o formulário com os dados do cliente
          if (c.birth_date) {
            const birthYear = new Date(c.birth_date).getFullYear();
            const age = new Date().getFullYear() - birthYear;
            setBioValue('age', age);
          }
          if (c.sex === 'male' || c.sex === 'female') setBioValue('sex', c.sex);
        }

        if (factorsRes.status === 'fulfilled') {
          setActivityFactors(factorsRes.value);
        }

        // Pré-preencher peso e altura com a avalição física mais recente
        if (
          assessmentRes.status === 'fulfilled' &&
          assessmentRes.value.length > 0
        ) {
          const latest = assessmentRes.value[0];
          if (latest.weight_kg) setBioValue('weight_kg', latest.weight_kg);
          if (latest.height_cm) setBioValue('height_cm', latest.height_cm);
        }
      } catch (error) {
        toast.error(
          error.response?.data?.detail ||
            'Erro ao carregar dados do cliente ou fatores de atividade.'
        );
      } finally {
        setLoadingClient(false);
      }
    };
    init();
  }, [id, setBioValue]);

  // Passo 1: Submeter dados biométricos -> obter TMB/TDEE
  const onBioSubmit = async (formValues) => {
    try {
      const payload = {
        weight_kg: parseFloat(formValues.weight_kg),
        height_cm: parseFloat(formValues.height_cm),
        age: parseInt(formValues.age, 10),
        sex: formValues.sex,
        activity_key: formValues.activity_key,
      };
      const result = await calculateMacros(payload);
      setCalcResult(result);
      setKcalAdjust(0); // reset do ajuste manual
      setStep(2); // Avança para o passo 2
    } catch (error) {
      toast.error(
        error.response?.data?.detail ||
          'Erro ao calcular. Verifica os dados inseridos.'
      );
    }
  };

  // Passo 2: Personal Trainer selecciona o TDEE que quer usar
  const handleSelectTdee = (formula) => {
    setSelectedTdee(formula.tdee);
    setSelectedFormulaLabel(formula.label); // limpa resultados anteriores ao mudar de TDEE
    setKcalAdjust(0);
    setMacroDistribution(null);
    setStep(3); // Avança para o passo 3
  };

  // Passo 3: Calcular distribuição de macros
  const onMacroSubmit = async (formValues) => {
    if (!adjustedKcal || !calcResult) return;
    setCalculatingMacros(true);
    try {
      const basePayload = {
        weight_kg: calcResult.weight_kg,
        height_cm: calcResult.height_cm,
        age: calcResult.age,
        sex: calcResult.sex,
        activity_key: calcResult.activity_key,
        kcal_target: adjustedKcal,
        method: macroMethod,
      };

      const macroPayload =
        macroMethod === 'percentages'
          ? {
              ...basePayload,
              protein_pct: parseFloat(formValues.protein_pct),
              carbs_pct: parseFloat(formValues.carbs_pct),
              fats_pct: parseFloat(formValues.fats_pct),
            }
          : {
              ...basePayload,
              protein_g_per_kg: parseFloat(formValues.protein_g_per_kg),
              carbs_g_per_kg: parseFloat(formValues.carbs_g_per_kg),
              fats_g_per_kg: parseFloat(formValues.fats_g_per_kg),
            };

      const result = await calculateMacros(macroPayload);
      setMacroDistribution(result.macro_distribution);
    } catch (error) {
      toast.error(
        error.response?.data?.detail ||
          'Erro ao calcular distribuição de macros. Verifica os dados inseridos.'
      );
    } finally {
      setCalculatingMacros(false);
    }
  };

  // Aplica um preset de percentagens ao formulário
  const applyPreset = (preset) => {
    resetMacroForm({
      ...preset,
      protein_g_per_kg: watchMacro('protein_g_per_kg'), // mantém os valores de g/kg
      carbs_g_per_kg: watchMacro('carbs_g_per_kg'),
      fats_g_per_kg: watchMacro('fats_g_per_kg'),
    });
    setMacroMethod(null); // força o utilizador a escolher um método (evita confusão)
  };

  // Navega para planos alimentares com o cliente pré-seleccionado
  const handleGoToPlans = () => {
    const calculatorTargets = macroDistribution
      ? {
          kcal: macroDistribution.kcal_target,
          protein_g: macroDistribution.protein_g,
          carbs_g: macroDistribution.carbs_g,
          fats_g: macroDistribution.fats_g,
        }
      : null;

    navigate('/trainer/planos-alimentares', {
      state: {
        preselectedClientId: id,
        fromCalculator: true, // flag para indicar que estamos a vir da calculadora
        calculatorTargets, // passa os resultados da calculadora para pré-preencher o plano
      },
    });
  };

  const handleBack = () => {
    if (cameFromMealPlans) {
      navigate('/trainer/planos-alimentares', {
        state: {
          preselectedClientId: id,
        },
      });
      return;
    }

    navigate(`/trainer/clientes/${id}`);
  };

  // Reiniciar o fluxo desde o início
  const handleReset = () => {
    setStep(1);
    setCalcResult(null);
    setSelectedTdee(null);
    setKcalAdjust(0);
    setMacroDistribution(null);
  };

  // Loading
  if (loadingClient) {
    return (
      <div className="p-4 lg:p-6 space-y-4">
        <div className="h-8 w-64 rounded bg-card animate-pulse" />
        <div className="h-64 rounded-xl bg-card animate-pulse" />
      </div>
    );
  }

  // Render

  return (
    <div className="p-4 lg:p-6 space-y-6 max-w-2xl">
      {/* Cabeçalho */}
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={handleBack}
          className="text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-lg font-semibold text-foreground flex items-center gap-2">
            <Calculator className="h-5 w-5 text-muted-foreground" />
            Calculadora de Macros
          </h1>
          {client && (
            <p className="text-sm text-muted-foreground">{client.full_name}</p>
          )}
        </div>
      </div>

      {/* Indicador de progresso */}
      <div className="flex items-center gap-2 text-sm">
        {['Dados', 'TDEE', 'Macros'].map((label, idx) => {
          const n = idx + 1;
          const isActive = step === n;
          const isDone = step > n;
          return (
            <div key={n} className="flex items-center gap-2">
              <div
                className={`flex items-center gap-1.5 ${
                  isActive
                    ? 'text-primary font-medium'
                    : isDone
                      ? 'text-muted-foreground'
                      : 'text-muted-foreground/40'
                }`}
              >
                <span
                  className={`h-5 w-5 rounded-full text-xs flex items-center justify-center font-semibold border ${
                    isActive
                      ? 'border-primary bg-primary text-primary-foreground'
                      : isDone
                        ? 'border-muted-foreground/40 bg-muted text-muted-foreground'
                        : 'border-muted-foreground/20 text-muted-foreground/40'
                  }`}
                >
                  {isDone ? '✓' : n}
                </span>
                {label}
              </div>
              {idx < 2 && (
                <ChevronRight className="h-3.5 w-3.5 text-muted-foreground/30" />
              )}
            </div>
          );
        })}

        {/* Botão de reinício — aparece a partir do passo 2 */}
        {step > 1 && (
          <button
            onClick={handleReset}
            className="ml-auto text-xs text-muted-foreground hover:text-foreground flex items-center gap-1"
          >
            <RefreshCw className="h-3 w-3" /> Recomeçar
          </button>
        )}
      </div>

      {/* PASSO 1: Dados biométricos */}
      {step === 1 && (
        <Card className="bg-card border-border">
          <CardContent className="p-5 space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-medium text-foreground">
                Dados Biométricos
              </h2>
              {/* Indicação de pré-preenchimento automático */}
              <span className="text-xs text-muted-foreground bg-muted px-2 py-0.5 rounded-full">
                Pré-preenchido da avaliação física
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="weight_kg">
                  Peso (kg) <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="weight_kg"
                  type="number"
                  step="0.1"
                  placeholder="Ex: 75.5"
                  {...regBio('weight_kg', {
                    required: 'Obrigatório',
                    min: { value: 20, message: 'Mín. 20kg' },
                    max: { value: 500, message: 'Máx. 500kg' },
                  })}
                />
                {bioErrors.weight_kg && (
                  <p className="text-xs text-destructive">
                    {bioErrors.weight_kg.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="height_cm">
                  Altura (cm) <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="height_cm"
                  type="number"
                  placeholder="Ex: 175"
                  {...regBio('height_cm', {
                    required: 'Obrigatório',
                    min: { value: 50, message: 'Mín. 50cm' },
                    max: { value: 300, message: 'Máx. 300cm' },
                  })}
                />
                {bioErrors.height_cm && (
                  <p className="text-xs text-destructive">
                    {bioErrors.height_cm.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="age">
                  Idade <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="age"
                  type="number"
                  placeholder="Ex: 28"
                  {...regBio('age', {
                    required: 'Obrigatório',
                    min: { value: 5, message: 'Mín. 5 anos' },
                    max: { value: 120, message: 'Máx. 120 anos' },
                  })}
                />
                {bioErrors.age && (
                  <p className="text-xs text-destructive">
                    {bioErrors.age.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="sex">Sexo</Label>
                <select
                  id="sex"
                  className="w-full h-9 rounded-md border border-input bg-background px-3 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                  {...regBio('sex')}
                >
                  <option value="male">Masculino</option>
                  <option value="female">Feminino</option>
                </select>
              </div>
            </div>

            {/* Nível de atividade */}
            <div className="space-y-1.5">
              <Label htmlFor="activity_key">Nível de Atividade</Label>
              <select
                id="activity_key"
                className="w-full h-9 rounded-md border border-input bg-background px-3 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                {...regBio('activity_key')}
              >
                {activityFactors.length > 0 ? (
                  activityFactors.map((f) => (
                    <option key={f.key} value={f.key}>
                      {f.label}
                    </option>
                  ))
                ) : (
                  <>
                    <option value="sedentary">Sedentário</option>
                    <option value="lightly_active">
                      Pouco ativo (1-3x/sem)
                    </option>
                    <option value="moderately_active">
                      Moderadamente ativo (3-5x/sem)
                    </option>
                    <option value="very_active">Muito ativo (6-7x/sem)</option>
                    <option value="extremely_active">Extremamente ativo</option>
                  </>
                )}
              </select>
            </div>

            <Button
              onClick={handleBioSubmit(onBioSubmit)}
              disabled={bioSubmitting}
              className="w-full"
            >
              {bioSubmitting ? 'A calcular...' : 'Calcular TMB / TDEE'}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* ── PASSO 2: Selecção do TDEE + ajuste ────────── */}
      {step === 2 && calcResult && (
        <div className="space-y-4">
          {/* Resumo dos dados usados */}
          <Card className="bg-card border-border">
            <CardContent className="p-4">
              <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                <span>{calcResult.weight_kg} kg</span>
                <span>·</span>
                <span>{calcResult.height_cm} cm</span>
                <span>·</span>
                <span>{calcResult.age} anos</span>
                <span>·</span>
                <span>
                  {calcResult.sex === 'male' ? 'Masculino' : 'Feminino'}
                </span>
                <span>·</span>
                <span>{calcResult.activity_label}</span>
                <span className="text-xs">(×{calcResult.activity_factor})</span>
              </div>
            </CardContent>
          </Card>

          <h2 className="text-sm font-medium text-foreground">
            Selecciona o TDEE alvo
          </h2>
          <p className="text-xs text-muted-foreground -mt-2">
            Clica na fórmula que queres usar como base calórica.
          </p>

          <div className="space-y-3">
            {calcResult.formulas.map((formula) => (
              <button
                key={formula.formula}
                type="button"
                onClick={() => handleSelectTdee(formula)}
                className="w-full text-left border border-border rounded-xl p-4 hover:border-primary hover:bg-primary/5 transition-colors group"
              >
                <div className="flex items-center justify-between">
                  <div className="space-y-1">
                    <p className="text-sm font-medium text-foreground group-hover:text-primary transition-colors">
                      {formula.label}
                    </p>
                    <div className="flex gap-4 text-sm text-muted-foreground">
                      <span>
                        TMB:{' '}
                        <strong className="text-foreground">
                          {formula.tmb} kcal
                        </strong>
                      </span>
                      <span>
                        TDEE:{' '}
                        <strong className="text-foreground">
                          {formula.tdee} kcal
                        </strong>
                      </span>
                    </div>
                  </div>
                  <ChevronRight className="h-4 w-4 text-muted-foreground group-hover:text-primary shrink-0" />
                </div>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* PASSO 3: Distribuição de macros  */}
      {step === 3 && selectedTdee && (
        <div className="space-y-4">
          {/* TDEE seleccionado + campo de ajuste */}
          <Card className="bg-card border-border">
            <CardContent className="p-4 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs text-muted-foreground">
                    TDEE seleccionado
                  </p>
                  <p className="text-lg font-bold text-foreground">
                    {selectedTdee} kcal
                    <span className="text-sm font-normal text-muted-foreground ml-2">
                      ({selectedFormulaLabel})
                    </span>
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setStep(2)}
                  className="text-muted-foreground"
                >
                  Alterar
                </Button>
              </div>

              {/*
               * Ajuste de calorias: défice (negativo) ou superavit (positivo).
               * O valor final (TDEE + ajuste) é usado como kcal_target na API.
               * Apresentamos o resultado em tempo real para o Personal Trainer confirmar
               * antes de calcular a distribuição.
               */}
              <div className="space-y-1.5">
                <Label
                  htmlFor="kcal-adjust"
                  className="flex items-center justify-between"
                >
                  <span>Ajuste de kcal (défice / superávit)</span>
                  <span className="text-xs text-muted-foreground font-normal">
                    Use negativo para défice
                  </span>
                </Label>
                <div className="flex gap-2 items-center">
                  <Input
                    id="kcal-adjust"
                    type="number"
                    step="50"
                    placeholder="Ex: -500 ou +300"
                    value={kcalAdjust === 0 ? '' : kcalAdjust}
                    onChange={(e) => {
                      setKcalAdjust(parseFloat(e.target.value) || 0);
                      setMacroDistribution(null);
                    }}
                    className="flex-1"
                  />
                  <span className="text-sm text-muted-foreground shrink-0">
                    kcal
                  </span>
                </div>
              </div>

              {/* Total ajustado — destaque visual com cor por intenção */}
              <div
                className={`flex items-center justify-between p-3 rounded-lg ${
                  kcalAdjust < 0
                    ? 'bg-blue-500/10 border border-blue-500/20'
                    : kcalAdjust > 0
                      ? 'bg-orange-500/10 border border-orange-500/20'
                      : 'bg-muted'
                }`}
              >
                <span className="text-sm text-muted-foreground">
                  {kcalAdjust < 0
                    ? 'Défice calórico'
                    : kcalAdjust > 0
                      ? 'Superávit calórico'
                      : 'Manutenção'}
                </span>
                <span className="text-xl font-bold text-primary">
                  {adjustedKcal} kcal
                </span>
              </div>
            </CardContent>
          </Card>

          {/* Seletor de método de distribuição de macros */}
          <div className="flex gap-2">
            {['percentages', 'grams_per_kg'].map((method) => (
              <button
                key={method}
                type="button"
                onClick={() => {
                  setMacroMethod(method);
                  setMacroDistribution(null);
                }}
                className={`flex-1 py-2 rounded-lg border text-sm font-medium transition-colors ${
                  macroMethod === method
                    ? 'border-primary bg-primary/10 text-primary'
                    : 'border-border text-muted-foreground hover:border-muted-foreground'
                }`}
              >
                {method === 'percentages' ? 'Por % de kcal' : 'Por g/kg'}
              </button>
            ))}
          </div>

          {/* Formulário de distribuição */}
          <Card className="bg-card border-border">
            <CardContent className="p-5 space-y-4">
              {macroMethod === 'percentages' ? (
                <>
                  {/* Presets */}
                  <div className="space-y-2">
                    <p className="text-xs text-muted-foreground">
                      Sugestões por objetivo:
                    </p>
                    <div className="flex flex-wrap gap-2">
                      {MACRO_PRESETS.map((preset) => (
                        <button
                          key={preset.label}
                          type="button"
                          onClick={() => applyPreset(preset)}
                          className="text-xs px-2.5 py-1 rounded-full border border-border text-muted-foreground hover:border-primary hover:text-primary transition-colors"
                        >
                          {preset.label}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="grid grid-cols-3 gap-3">
                    {[
                      { id: 'protein_pct', label: 'Proteína (%)' },
                      { id: 'carbs_pct', label: 'HC (%)' },
                      { id: 'fats_pct', label: 'Gordura (%)' },
                    ].map(({ id, label }) => (
                      <div key={id} className="space-y-1.5">
                        <Label htmlFor={id} className="text-xs">
                          {label}
                        </Label>
                        <Input
                          id={id}
                          type="number"
                          min="0"
                          max="100"
                          {...regMacro(id, {
                            required: 'Obrigatório',
                            onChange: () => setMacroDistribution(null),
                          })}
                        />
                      </div>
                    ))}
                  </div>

                  <p
                    className={`text-xs font-medium ${
                      Math.abs(pctSum - 100) < 0.5
                        ? 'text-green-600 dark:text-green-400'
                        : 'text-destructive'
                    }`}
                  >
                    Soma: {pctSum.toFixed(0)}%
                    {Math.abs(pctSum - 100) < 0.5 ? ' ✓' : ' — deve ser 100%'}
                  </p>
                </>
              ) : (
                /* Método g/kg — com preview de gramas em tempo real */
                <div className="space-y-4">
                  <div className="grid grid-cols-3 gap-3">
                    {[
                      {
                        id: 'carbs_g_per_kg',
                        label: 'HC (g/kg)',
                        hint: '3.0–6.0',
                      },
                      {
                        id: 'protein_g_per_kg',
                        label: 'Proteína (g/kg)',
                        hint: '1.6–2.2',
                      },

                      {
                        id: 'fats_g_per_kg',
                        label: 'Gordura (g/kg)',
                        hint: '0.5–1.5',
                      },
                    ].map(({ id, label, hint }) => (
                      <div key={id} className="space-y-1.5">
                        <Label htmlFor={id} className="text-xs">
                          {label}
                        </Label>
                        <Input
                          id={id}
                          type="number"
                          step="0.1"
                          min="0"
                          placeholder={hint}
                          {...regMacro(id, {
                            required: 'Obrigatório',
                            onChange: () => setMacroDistribution(null),
                          })}
                        />
                        <p className="text-xs text-muted-foreground">{hint}</p>
                      </div>
                    ))}
                  </div>

                  {/*
                   * Preview em tempo real das gramas absolutas.
                   * Fórmula: gramas = g/kg × peso_cliente
                   * Actualiza a cada keystroke — sem round-trip à API.
                   * Permite ao Personal Trainer confirmar os valores antes de submeter.
                   */}
                  {weightKg > 0 && (
                    <div className="p-3 rounded-lg bg-muted border border-border">
                      <p className="text-xs text-muted-foreground mb-2 font-medium">
                        Preview para {weightKg} kg:
                      </p>
                      <div className="flex gap-3 text-sm flex-wrap">
                        <span className="text-orange-600 dark:text-orange-400 font-semibold">
                          HC: {gramsFromRatio.carbs}g
                        </span>
                        <span className="text-blue-600 dark:text-blue-400 font-semibold">
                          P: {gramsFromRatio.protein}g
                        </span>
                        <span className="text-yellow-600 dark:text-yellow-400 font-semibold">
                          G: {gramsFromRatio.fats}g
                        </span>
                        <span className="text-green-600 dark:text-green-400 font-semibold">
                          ≈ {kcalFromRatio} kcal
                        </span>
                      </div>
                    </div>
                  )}
                </div>
              )}

              <Button
                onClick={handleMacroSubmit(onMacroSubmit)}
                disabled={
                  calculatingMacros ||
                  (macroMethod === 'percentages' && Math.abs(pctSum - 100) >= 1)
                }
                className="w-full"
              >
                {calculatingMacros ? 'A calcular...' : 'Calcular Distribuição'}
              </Button>
            </CardContent>
          </Card>

          {/* Resultado da distribuição de macronutrientes */}
          {macroDistribution && (
            <div className="space-y-4">
              <div className="grid grid-cols-3 gap-3">
                <MacroResultCard
                  label="Hidratos"
                  grams={macroDistribution.carbs_g}
                  gPerKg={macroDistribution.carbs_g_per_kg}
                  pct={macroDistribution.carbs_pct}
                  color="orange"
                />
                <MacroResultCard
                  label="Proteína"
                  grams={macroDistribution.protein_g}
                  gPerKg={macroDistribution.protein_g_per_kg}
                  pct={macroDistribution.protein_pct}
                  color="blue"
                />
                <MacroResultCard
                  label="Gordura"
                  grams={macroDistribution.fats_g}
                  gPerKg={macroDistribution.fats_g_per_kg}
                  pct={macroDistribution.fats_pct}
                  color="yellow"
                />
              </div>

              <Card className="bg-card border-border">
                <CardContent className="p-4 flex items-center justify-between">
                  <div>
                    <p className="text-xs text-muted-foreground">
                      Total calórico dos macros
                    </p>
                    <p className="text-xl text-green-600 dark:text-green-400 font-bold">
                      {macroDistribution.kcal_from_macros} kcal
                    </p>
                    {Math.abs(
                      macroDistribution.kcal_from_macros - adjustedKcal
                    ) > 10 && (
                      <p className="text-xs text-muted-foreground mt-0.5">
                        Diferença de{' '}
                        {Math.abs(
                          Math.round(
                            macroDistribution.kcal_from_macros - adjustedKcal
                          )
                        )}{' '}
                        kcal face ao objectivo
                      </p>
                    )}
                  </div>
                  <CheckCircle2 className="h-6 w-6 text-green-500 shrink-0" />
                </CardContent>
              </Card>

              <Button onClick={handleGoToPlans} className="w-full" size="lg">
                Ir para Planos Alimentares
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
