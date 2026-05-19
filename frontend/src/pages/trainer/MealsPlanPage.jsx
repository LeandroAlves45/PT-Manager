/**
 * MealPlanPage.jsx — Gestão de planos alimentares do Personal Trainer (NR-01)
 *
 * Rota: /trainer/planos-alimentares
 *
 * Três modos de visualização controlados por `view`:
 *   "list"    — seletor de cliente + lista de planos existentes
 *   "builder" — formulário de criação de um novo plano
 *   "editor"  — edição das refeições de um plano existente
 */

import { useState, useEffect, useMemo, useCallback } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';

import { getClients } from '@/api/clientsApi';
import {
  getFoods,
  getMealPlansByClient,
  createMealPlan,
  archiveMealPlan,
  unarchiveMealPlan,
  updateMealPlanMeals,
} from '@/api/nutritionApi';
import { getSupplements } from '@/api/supplementApi';

import { matchesSearch } from '@/utils/validators';
import { formatDate } from '@/utils/formatters';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Plus,
  Trash2,
  ArrowLeft,
  UtensilsCrossed,
  Search,
  ChevronDown,
  ChevronUp,
  Archive,
  CheckCircle2,
  Pencil,
  Pill,
  Calculator,
  X,
  RotateCcw,
} from 'lucide-react';

// ============================================================
// Utilitário: cálculo local de macros
// ============================================================

/**
 * Calcula macros de um alimento para uma dada quantidade em gramas.
 * Replica a fórmula do backend em Python (_calculate_item_macros).
 * Permite que o frontend mostre totais em tempo real sem round-trips ao servidor.
 *
 * Fórmula: macro_real = (macro_por_100g / 100) * quantity_grams
 * kcal = protein*4 + carbs*4 + fats*9
 */
function calcItemMacros(food, quantityGrams) {
  const q = parseFloat(quantityGrams) || 0; // Garantir que é número, default 0
  const factor = q / 100; // Fator de conversão para porção
  const protein_g = Math.round((food.protein || 0) * factor * 100) / 100;
  const carbs_g = Math.round((food.carbs || 0) * factor * 100) / 100;
  const fats_g = Math.round((food.fats || 0) * factor * 100) / 100;
  const kcal =
    Math.round((protein_g * 4 + carbs_g * 4 + fats_g * 9) * 100) / 100;
  return { protein_g, carbs_g, fats_g, kcal };
}

/**
 * Agrega macros de uma lista de items (cada item tem { food, quantity_grams }).
 * Devolve MacroSummary: { protein_g, carbs_g, fats_g, kcal }.
 */
function sumMacros(items) {
  return items.reduce(
    (acc, item) => {
      const m = calcItemMacros(item.food, item.quantity_grams);
      return {
        protein_g: Math.round((acc.protein_g + m.protein_g) * 100) / 100,
        carbs_g: Math.round((acc.carbs_g + m.carbs_g) * 100) / 100,
        fats_g: Math.round((acc.fats_g + m.fats_g) * 100) / 100,
        kcal: Math.round((acc.kcal + m.kcal) * 100) / 100,
      };
    },
    { protein_g: 0, carbs_g: 0, fats_g: 0, kcal: 0 }
  );
}

// ============================================================
// Sub-componente: cartão de macro totais
// ============================================================

/**
 * MacroProgressCard — exibe o progresso dos macros em 3 colunas.
 *
 * Aparece no topo do builder assim que há alimentos inseridos.
 * Sem targets definidos, mostra apenas a coluna "Total" (sem Objetivo/Falta).
 *
 * targets: { kcal, protein_g, carbs_g, fats_g } — todos opcionais
 * actual:  MacroSummary — calculado em tempo real a partir dos meals
 */
function MacroProgressCard({ macros: actual, targets = {} }) {
  const hasTargets =
    targets.kcal || targets.protein_g || targets.carbs_g || targets.fats_g;

  function DiffCell({ actual, target, unit = 'g' }) {
    if (!target) return <span className="text-muted-foreground">—</span>;
    const diff = Math.round(actual - target);
    const isOk = Math.abs(diff) <= (unit === 'kcal' ? 50 : 5); // margem de erro aceitável
    return (
      <span
        className={`font-semibold text-sm ${
          isOk
            ? 'text-green-500'
            : diff > 0
              ? 'text-orange-500'
              : 'text-blue-500'
        }`}
      >
        {diff > 0 ? `+${diff}` : diff} {unit}
      </span>
    );
  }

  const rows = [
    {
      label: 'kcal',
      actual: Math.round(actual.kcal),
      target: targets.kcal,
      unit: 'kcal',
      color: 'text-green-500 dark:text-green-400',
    },
    {
      label: 'Hidratos',
      actual: Math.round(actual.carbs_g),
      target: targets.carbs_g,
      unit: 'g',
      color: 'text-orange-500 dark:text-orange-400',
    },
    {
      label: 'Proteína',
      actual: Math.round(actual.protein_g),
      target: targets.protein_g,
      unit: 'g',
      color: 'text-blue-500 dark:text-blue-400',
    },
    {
      label: 'Gordura',
      actual: Math.round(actual.fats_g),
      target: targets.fats_g,
      unit: 'g',
      color: 'text-yellow-500 dark:text-yellow-400',
    },
  ];

  return (
    <Card className="bg-card border-border">
      <CardContent className="p-4">
        {/* Cabeçalhos das colunas */}
        <div
          className={`grid gap-2 text-xs text-center mb-2 ${
            hasTargets ? 'grid-cols-3' : 'grid-cols-1'
          }`}
        >
          {hasTargets && (
            <span className="text-muted-foreground font-medium uppercase">
              Objetivo
            </span>
          )}
          <span className="text-muted-foreground font-medium uppercase">
            Total atual
          </span>
          {hasTargets && (
            <span className="text-muted-foreground font-medium uppercase">
              Falta
            </span>
          )}
        </div>

        {/* Linhas de macros */}
        <div className="space-y-1.5">
          {rows.map(({ label, actual: a, target, unit, color }) => (
            <div
              key={label}
              className={`grid gap-2 text-sm items-center ${
                hasTargets ? 'grid-cols-3' : 'grid-cols-1'
              }`}
            >
              {hasTargets && (
                // Objetivo: usa a mesma cor da macro para consistência visual.
                <span className={`text-center font-semibold text-xs ${color}`}>
                  {target ? `${Math.round(target)} ${unit}` : '—'}
                </span>
              )}
              <span className={`text-center font-semibold ${color}`}>
                {a} {unit}
                <span className="text-muted-foreground font-normal ml-1 text-xs">
                  {label}
                </span>
              </span>
              {hasTargets && (
                <span className="text-center">
                  <DiffCell actual={a} target={target} unit={unit} />
                </span>
              )}
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================================
// Sub-componente: cartão de macro totais
// ============================================================

/**
 * MacroBar — exibe 4 valores (HC, P, G, kcal) em linha.
 * Reutilizado nos totais de refeição e do plano.
 */
function MacroBar({ macros, label, compact = false }) {
  const { carbs_g = 0, protein_g = 0, fats_g = 0, kcal = 0 } = macros || {};
  return (
    <div className={`flex flex-wrap gap-2 ${compact ? 'text-xs' : 'text-sm'}`}>
      {label && (
        <span className="text-muted-foreground font-medium w-full text-xs">
          {label}
        </span>
      )}
      {/* HC = Hidratos de Carbono — convenção PT-PT */}
      <span className="px-2 py-0.5 rounded bg-orange-500/10 text-orange-600 dark:text-orange-400 font-medium">
        HC {carbs_g}g
      </span>
      <span className="px-2 py-0.5 rounded bg-blue-500/10 text-blue-600 dark:text-blue-400 font-medium">
        P {protein_g}g
      </span>
      <span className="px-2 py-0.5 rounded bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 font-medium">
        G {fats_g}g
      </span>
      <span className="px-2 py-0.5 rounded bg-green-500/10 text-green-600 dark:text-green-400 font-semibold">
        {kcal} kcal
      </span>
    </div>
  );
}

// ============================================================
// Sub-componente: refeição individual no builder
// ============================================================

/**
 * MealBuilder — um bloco de refeição no builder.
 *
 * Props:
 *   meal              — { localId, name, items: [...], supplements: [{ supplement_id, notes }] }
 *   foods             — catálogo completo (para pesquisa de alimentos)
 *   onUpdate          — callback (updatedMeal) → atualiza o estado no pai
 *   onRemove          — callback () → remove esta refeição
 *   isOnly            — true se for a única refeição (impede remoção)
 *   clientSupplements — suplementos atribuídos ao cliente (para o picker)
 */
function MealBuilder({
  meal,
  foods,
  onUpdate,
  onRemove,
  isOnly,
  clientSupplements = [],
}) {
  // Estado local: query de pesquisa de alimentos e expansão do picker
  const [foodSearch, setFoodSearch] = useState('');
  const [pickerOpen, setPickerOpen] = useState(false);
  const [suppPickerOpen, setSuppPickerOpen] = useState(false);

  // Filtra o catálogo de alimentos com matchesSearch
  const filteredFoods = useMemo(() => {
    if (!foodSearch.trim()) return foods.slice(0, 20); // Mostrar os 20 primeiros se sem busca
    return foods.filter((food) => matchesSearch(foodSearch, food.name));
  }, [foodSearch, foods]);

  // Adiciona um alimento á refeição (com quantidade default 100g)
  const handleAddFood = useCallback(
    (food) => {
      const newItem = {
        localId: `${food.id}-${Date.now()}`, // chave React única (não vai para a API)
        food,
        quantity_grams: 100,
      };
      onUpdate({ ...meal, items: [...meal.items, newItem] });
      setFoodSearch('');
      setPickerOpen(false);
    },
    [meal, onUpdate]
  );

  // Atualiza a quantidade de um item existente
  const handleQuantityChange = useCallback(
    (localId, value) => {
      const updated = meal.items.map((item) =>
        item.localId === localId ? { ...item, quantity_grams: value } : item
      );
      onUpdate({ ...meal, items: updated });
    },
    [meal, onUpdate]
  );

  // Remove um item da refeição
  const handleRemoveItem = useCallback(
    (localId) => {
      onUpdate({
        ...meal,
        items: meal.items.filter((item) => item.localId !== localId),
      });
    },
    [meal, onUpdate]
  );

  // Toggle de suplemento - adiciona se não existir, remove se já estiver
  const handleToggleSupplement = useCallback(
    (suppId) => {
      const current = meal.supplements || [];
      const exists = current.some((s) => s.supplement_id === suppId);
      const updated = exists
        ? current.filter((s) => s.supplement_id !== suppId)
        : [...current, { supplement_id: suppId, notes: '' }];
      onUpdate({ ...meal, supplements: updated });
    },
    [meal, onUpdate]
  );

  // Atualiza as notas de um suplemento específico desta refeição
  const handleUpdateSupplementNotes = useCallback(
    (suppId, notes) => {
      const updated = (meal.supplements || []).map((s) =>
        s.supplement_id === suppId ? { ...s, notes } : s
      );
      onUpdate({ ...meal, supplements: updated });
    },
    [meal, onUpdate]
  );

  // Atualiza o nome da refeição
  const handleNameChange = (e) => {
    onUpdate({ ...meal, name: e.target.value });
  };

  // Macros totais da refeição, recalculados a cada mudança
  const mealMacros = useMemo(() => sumMacros(meal.items), [meal.items]);

  return (
    <div className="border border-border rounded-xl p-4 space-y-3 bg-card">
      {/* Cabeçalho da refeição: nome editável + botão remover */}
      <div className="flex items-center gap-2">
        <Input
          value={meal.name}
          onChange={handleNameChange}
          placeholder="Nome da refeição"
          className="font-medium text-sm h-8 flex-1"
        />

        {/* Botão de suplementos - só aparece se o cliente tiver suplementos atribuídos.
            Badge numérico indica quantos suplementos estão associados a esta refeição.
            Fica colorido (text-primary) quando há suplementos selecionados. */}
        {clientSupplements.length > 0 && (
          <div className="relative shrink-0">
            <Button
              variant="ghost"
              size="icon"
              className={`h-8 w-8 transition-colors ${
                (meal.supplements || []).length > 0
                  ? 'text-primary hover:text-primary/80'
                  : 'text-muted-foreground hover:text-primary'
              }`}
              onClick={() => setSuppPickerOpen((prev) => !prev)}
              title="Suplementos desta refeição"
            >
              <Pill className="h-3.5 w-3.5" />
            </Button>
            {/* Badge com contagem de suplementos selecionados */}
            {(meal.supplements || []).length > 0 && (
              <span className="absolute -top-1 -right-1 h-4 w-4 rounded-full bg-primary text-primary-foreground text-[10px] font-bold flex items-center justify-center pointer-events-none">
                {meal.supplements.length}
              </span>
            )}
          </div>
        )}

        {!isOnly && (
          // Só mostra o botão de remover se existir mais do que uma refeição
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-muted-foreground hover:text-destructive shrink-0"
            onClick={onRemove}
            title="Remover refeição"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        )}
      </div>

      {/* Dropdown de suplementos — aparece logo abaixo do cabeçalho quando aberto */}
      {clientSupplements.length > 0 && suppPickerOpen && (
        <div className="border border-primary/20 rounded-lg bg-popover shadow-md">
          <div className="flex items-center justify-between px-3 py-2 border-b border-border">
            <p className="text-xs text-muted-foreground font-medium">
              Suplementos - clica para associar/remover
            </p>
            <button
              type="button"
              onClick={() => setSuppPickerOpen(false)}
              className="text-xs text-muted-foreground hover:text-foreground"
            >
              ✕
            </button>
          </div>
          <div className="max-h-40 overflow-y-auto">
            {clientSupplements.map((s) => {
              const isSelected = (meal.supplements || []).some(
                (supp) => supp.supplement_id === s.id
              );
              return (
                <button
                  key={s.id}
                  type="button"
                  className={`w-full flex items-center justify-between px-3 py-2 text-sm hover:bg-accent text-left transition-colors ${
                    isSelected ? 'bg-primary/5' : ''
                  }`}
                  onClick={() => handleToggleSupplement(s.id)}
                >
                  <span
                    className={`flex items-center gap-2 ${isSelected ? 'text-primary font-medium' : 'text-foreground'}`}
                  >
                    <Pill className="h-3 w-3 shrink-0" />
                    {s.name}
                    {s.timing && (
                      <span className="text-xs text-muted-foreground font-normal">
                        ({s.timing})
                      </span>
                    )}
                  </span>
                  <span
                    className={`text-xs ml-2 shrink-0 ${isSelected ? 'text-primary' : 'text-muted-foreground'}`}
                  >
                    {isSelected ? '✓' : '+'}
                  </span>
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* Suplementos selecionados — cada um com campo de notas editável */}
      {(meal.supplements || []).length > 0 && (
        <div className="space-y-1.5 pt-1">
          {meal.supplements.map((suppItem) => {
            const supp = clientSupplements.find(
              (s) => s.id === suppItem.supplement_id
            );
            if (!supp) return null;
            return (
              <div
                key={suppItem.supplement_id}
                className="flex items-center gap-2"
              >
                {/* Nome do suplemento */}
                <span className="flex items-center gap-1 text-xs text-primary font-medium shrink-0">
                  <Pill className="h-2.5 w-2.5" />
                  {supp.name}
                </span>
                {/* Input de notas (dose, timing, instruções) */}
                <Input
                  placeholder="Dose, timing, instruções..."
                  value={suppItem.notes || ''}
                  onChange={(e) =>
                    handleUpdateSupplementNotes(
                      suppItem.supplement_id,
                      e.target.value
                    )
                  }
                  className="h-6 text-xs flex-1 px-2"
                />
                {/* Remover */}
                <button
                  type="button"
                  onClick={() => handleToggleSupplement(suppItem.supplement_id)}
                  className="text-muted-foreground hover:text-destructive shrink-0"
                  title="Remover suplemento"
                >
                  <X className="h-3 w-3" />
                </button>
              </div>
            );
          })}
        </div>
      )}

      {/* Lista de alimentos já adicionados */}
      {meal.items.length > 0 && (
        <div className="space-y-1.5">
          {meal.items.map((item) => {
            const itemMacros = calcItemMacros(item.food, item.quantity_grams);
            return (
              <div
                key={item.localId}
                className="flex items-center gap-2 py-1.5 border-b border-border last:border-0"
              >
                {/* Nome do alimento */}
                <span className="flex-1 text-sm text-foreground truncate">
                  {item.food.name}
                </span>
                {/* Macros do item (compacto) */}
                <span className="hidden sm:flex gap-1 text-xs text-muted-foreground shrink-0">
                  <span className="text-orange-500">
                    {itemMacros.carbs_g}HC
                  </span>
                  <span>·</span>
                  <span className="text-blue-500">{itemMacros.protein_g}P</span>
                  <span>·</span>
                  <span className="text-yellow-500">{itemMacros.fats_g}G</span>
                  <span>·</span>
                  <span className="text-green-500">{itemMacros.kcal}kcal</span>
                </span>
                <div className="flex items-center gap-1 shrink-0">
                  <Input
                    type="number"
                    min="1"
                    max="7000"
                    value={item.quantity_grams}
                    onChange={(e) =>
                      handleQuantityChange(item.localId, e.target.value)
                    }
                    className="h-7 w-20 text-xs text-right"
                  />
                  <span className="text-xs text-muted-foreground">g</span>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 text-muted-foreground hover:text-destructive shrink-0"
                  onClick={() => handleRemoveItem(item.localId)}
                >
                  <Trash2 className="h-3 w-3" />
                </Button>
              </div>
            );
          })}
        </div>
      )}

      {/* Totais da refeição */}
      {meal.items.length > 0 && <MacroBar macros={mealMacros} compact />}

      {/* Picker de alimentos */}
      <div className="relative">
        <Button
          variant="outline"
          size="sm"
          className="w-full text-muted-foreground justify-start gap-2"
          onClick={() => setPickerOpen((prev) => !prev)}
        >
          <Plus className="h-3.5 w-3.5" />
          Adicionar alimento
          {pickerOpen ? (
            <ChevronUp className="h-3.5 w-3.5 ml-auto" />
          ) : (
            <ChevronDown className="h-3.5 w-3.5 ml-auto" />
          )}
        </Button>

        {pickerOpen && (
          /*
           * Picker inline (não é um modal):
           * abre directamente abaixo do botão, fecha ao clicar num alimento.
           */
          <div className="mt-1 border border-border rounded-lg bg-popover shadow-md z-10">
            <div className="p-2 border-b border-border">
              <div className="relative">
                <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                <Input
                  autoFocus
                  placeholder="Pesquisar alimento..."
                  value={foodSearch}
                  onChange={(e) => setFoodSearch(e.target.value)}
                  className="pl-7 h-8 text-sm"
                />
              </div>
            </div>
            <div className="max-h-52 overflow-y-auto">
              {filteredFoods.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-4">
                  Nenhum alimento encontrado.
                </p>
              ) : (
                filteredFoods.map((food) => (
                  <button
                    key={food.id}
                    type="button"
                    className="w-full flex items-center justify-between px-3 py-2 text-sm hover:bg-accent text-left transition-colors"
                    onClick={() => handleAddFood(food)}
                  >
                    <span className="text-foreground">{food.name}</span>
                    <span className="text-xs text-muted-foreground shrink-0 ml-2">
                      {food.kcal} kcal/100g
                    </span>
                  </button>
                ))
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ============================================================
// Componente principal
// ============================================================

// Refeições padrão ao iniciar o builder (6 slots vazios)
const DEFAULT_MEALS = [
  { localId: 'meal-1', name: 'Refeição 1', items: [], supplements: [] },
  { localId: 'meal-2', name: 'Refeição 2', items: [], supplements: [] },
  { localId: 'meal-3', name: 'Refeição 3', items: [], supplements: [] },
  { localId: 'meal-4', name: 'Refeição 4', items: [], supplements: [] },
  { localId: 'meal-5', name: 'Refeição 5', items: [], supplements: [] },
  { localId: 'meal-6', name: 'Refeição 6', items: [], supplements: [] },
];

export default function MealsPlanPage() {
  // Estado de vista
  const location = useLocation();
  const navigate = useNavigate();

  // Leitura do estado de navegação vindo da calculadora
  // fromCalculator=true → entrar diretamente no builder em vez da lista
  // preselectedClientId → cliente pré-selecionado (vindo da calculadora ou da lista)
  // calculatorTargets   → { kcal, protein_g, carbs_g, fats_g } para o MacroProgressCard
  const fromCalculator = location.state?.fromCalculator ?? false;
  const locationClientId = location.state?.preselectedClientId ?? '';
  const locationTargets = location.state?.calculatorTargets ?? null;

  // Estado de vista
  // Se chegámos da calculadora, abrimos o builder diretamente
  const [view, setView] = useState(fromCalculator ? 'builder' : 'list'); // 'list' ou 'builder'

  // Estado de lista
  const [clients, setClients] = useState([]);
  const [selectedClientId, setSelectedClientId] = useState(locationClientId);
  const [plans, setPlans] = useState([]);
  // Estado do catálogo de alimentos
  // Carregado uma vez e partilhado por todos os MealBuilders
  const [foods, setFoods] = useState([]);
  // Suplementos atribuídos ao cliente seleccionado.
  // Carregados sempre que o cliente muda — passados a todos os MealBuilders.
  const [clientSupplements, setClientSupplements] = useState([]);

  // Estado de carregamento
  const [loadingClients, setLoadingClients] = useState(true);
  const [loadingPlans, setLoadingPlans] = useState(false);

  // Estado do builder
  const [meals, setMeals] = useState(DEFAULT_MEALS); // estado local das refeições em construção/edição

  // Targets vindos da calculadora
  const [planTargets, setPlanTargets] = useState(locationTargets); // objetivos de macros vindos da calculadora (opcional)

  // Estado do plano em edição
  const [editingPlan, setEditingPlan] = useState(null); // plano a editar (null para criar novo)

  // Estado do AlertDialog para arquivar plano
  const [archiveTarget, setArchiveTarget] = useState(null); // plano a arquivar

  // Estado do AlertDialog para desarquivar plano
  const [unarchiveTarget, setUnarchiveTarget] = useState(null); // plano a desarquivar

  const [showArchived, setShowArchived] = useState(false); // alterna entre planos ativos e arquivados

  // Estado do builder
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: { name: '', notes: '', starts_date: '', ends_date: '' },
  });

  // Carregamento de clientes
  useEffect(() => {
    const fetchClients = async () => {
      try {
        const data = await getClients();
        // Filtra apenas clientes ativos (status = 'active')
        setClients(data.filter((client) => client.status === 'active'));
      } catch (error) {
        toast.error(
          error.response?.data?.detail || 'Erro ao carregar clientes'
        );
      } finally {
        setLoadingClients(false);
      }
    };
    fetchClients();
  }, []);

  // Carregamento do catálogo de alimentos
  useEffect(() => {
    const fetchFoods = async () => {
      try {
        const data = await getFoods(true); // apenas alimentos ativos
        setFoods(data);
      } catch (error) {
        toast.error(
          error.response?.data?.detail || 'Erro ao carregar alimentos'
        );
      }
    };
    fetchFoods();
  }, []);

  // Carregamento de planos e suplementos quando o cliente muda
  // Promise.allSettled: se os suplementos falharem, os planos ainda carregam.
  useEffect(() => {
    if (!selectedClientId) {
      setPlans([]);
      setClientSupplements([]);
      return;
    }
    const fetchClientData = async () => {
      setLoadingPlans(true);
      try {
        const [plansRes, suppsRes] = await Promise.allSettled([
          getMealPlansByClient(selectedClientId, showArchived),
          getSupplements(),
        ]);
        if (plansRes.status === 'fulfilled') setPlans(plansRes.value);
        else toast.error('Erro ao carregar planos alimentares.');
        // Suplementos do cliente para o picker
        if (suppsRes.status === 'fulfilled')
          setClientSupplements(suppsRes.value);
        // Falha silenciosa nos suplementos — o builder funciona sem eles
      } finally {
        setLoadingPlans(false);
      }
    };
    fetchClientData();
  }, [selectedClientId, showArchived]);

  // Macros totais do plano em construção
  const planMacros = useMemo(() => {
    const allItems = meals.flatMap((meal) => meal.items);
    return sumMacros(allItems);
  }, [meals]);

  // A API com include_archived=true pode devolver ativos + arquivados.
  // A UI precisa de listas mutuamente exclusivas por tab.
  const visiblePlans = useMemo(() => {
    return plans.filter((plan) =>
      showArchived ? Boolean(plan.archived_at) : !plan.archived_at
    );
  }, [plans, showArchived]);

  // Recarrega planos respeitando o filtro atual
  const reloadPlans = async (includeArchived = showArchived) => {
    const updated = await getMealPlansByClient(
      selectedClientId,
      includeArchived
    );
    setPlans(updated);
  };

  // Handlers do builder
  // Adiciona uma nova refeição vazia
  const handleAddMeal = () => {
    setMeals((prev) => [
      ...prev,
      {
        localId: `meal-${Date.now()}`, // ID único para React, não vai para a API
        name: `Refeição ${prev.length + 1}`,
        items: [],
        supplements: [],
      },
    ]);
  };

  // Remove uma refeição do plano
  const handleRemoveMeal = (localId) => {
    setMeals((prev) => prev.filter((meal) => meal.localId !== localId));
  };

  // Atualiza uma refeição (nome ou items)
  const handleUpdateMeal = useCallback((updatedMeal) => {
    setMeals((prev) =>
      prev.map((meal) =>
        meal.localId === updatedMeal.localId ? updatedMeal : meal
      )
    );
  }, []);

  // Navaga para o builder e limpa o estado anterior
  const handleStartCreate = () => {
    if (!selectedClientId) {
      toast.error('Selecciona um cliente antes de criar um plano.');
      return;
    }
    navigate(`/trainer/clientes/${selectedClientId}/calculadora`, {
      state: {
        fromMealPlans: true,
        preselectedClientId: selectedClientId,
      },
    });
  }; // redireciona para a calculadora, que tem um botão para entrar no builder com targets

  const handleCancelCreate = () => {
    setView('list');
    setEditingPlan(null);
    setPlanTargets(null);
    reset();
    setMeals(DEFAULT_MEALS);
  };

  // Handlers do editor
  const handleStartEdit = (plan) => {
    setEditingPlan(plan);
    // Reconstrói o estado de refeições do builder a partir do plano existente
    const loadedMeals = plan.meals.map((meal) => ({
      localId: meal.id, // usar ID real para facilitar updates
      name: meal.name,
      supplements: (meal.supplements || []).map((s) => ({
        supplement_id: s.supplement_id,
        notes: s.notes || '',
      })),
      items: meal.items.map((item) => ({
        localId: `${item.food_id}-${item.id}`,
        food: {
          id: item.food_id,
          name: item.food_name,
          // macros por 100g: recalculados a partir da quantidade real para mostrar no builder
          protein:
            item.quantity_grams > 0
              ? (item.protein_g / item.quantity_grams) * 100
              : 0,
          carbs:
            item.quantity_grams > 0
              ? (item.carbs_g / item.quantity_grams) * 100
              : 0,
          fats:
            item.quantity_grams > 0
              ? (item.fats_g / item.quantity_grams) * 100
              : 0,
          kcal:
            item.quantity_grams > 0
              ? (item.kcal / item.quantity_grams) * 100
              : 0,
        },
        quantity_grams: item.quantity_grams,
      })),
    }));
    setMeals(
      loadedMeals.length > 0
        ? loadedMeals
        : [
            {
              localId: `meal-new-1`,
              name: 'Refeição 1',
              items: [],
              supplements: [],
            },
          ]
    );
    reset({
      name: plan.name,
      notes: plan.notes || '',
      starts_date: plan.starts_date || '',
      ends_date: plan.ends_date || '',
    });
    setView('editor');
  };

  // Guarda as alterações ao plano existente via PUT /meal-plans/:id/meals
  const handleSaveEdit = async (formValues) => {
    if (!editingPlan) return;
    const hasAnyFood = meals.some((m) => m.items.length > 0);
    if (!hasAnyFood) {
      toast.error('Adiciona pelo menos um alimento a uma refeição.');
      return;
    }
    const payload = {
      meals: meals
        .filter((m) => m.items.length > 0)
        .map((meal, idx) => ({
          name: meal.name.trim() || `Refeição ${idx + 1}`,
          order_index: idx,
          supplements: (meal.supplements || []).map((s) => ({
            supplement_id: s.supplement_id,
            notes: s.notes || null,
          })),
          items: meal.items.map((item) => ({
            food_id: item.food.id,
            quantity_grams: parseFloat(item.quantity_grams) || 100,
          })),
        })),
    };

    try {
      await updateMealPlanMeals(editingPlan.id, payload);
      toast.success('Plano atualizado com sucesso!');
      await reloadPlans();
      setView('list');
      setEditingPlan(null);
    } catch (err) {
      toast.error(err?.response?.data?.detail || 'Erro ao atualizar plano.');
    }
  };

  // Submete o plano via API
  // handleSubmit do react-hook-form valida os campos do cabeçalho antes de chamar esta função
  const onSubmit = async (formValues) => {
    if (!selectedClientId) {
      toast.error('Selecciona um cliente antes de guardar.');
      return;
    }

    // Valida que pelo menos uma refeição tem alimentos
    const hasAnyFood = meals.some((m) => m.items.length > 0);
    if (!hasAnyFood) {
      toast.error('Adiciona pelo menos um alimento a uma refeição.');
      return;
    }

    // Constrói payload no formato MealPlanCreate esperado pela API
    // Campos opcionais só são incluídos se tiverem valor (evita enviar null)
    const payload = {
      client_id: selectedClientId,
      name: formValues.name.trim(),
      active: true, // novos planos são ativos por defeito
      meals: meals
        .filter((meal) => meal.items.length > 0) // só enviar refeições com alimentos
        .map((meal, idx) => ({
          name: meal.name.trim() || `Refeição ${idx + 1}`,
          order_index: idx,
          supplements: (meal.supplements || []).map((s) => ({
            supplement_id: s.supplement_id,
            notes: s.notes || null,
          })),
          items: meal.items.map((item) => ({
            food_id: item.food.id,
            quantity_grams: parseFloat(item.quantity_grams) || 100,
          })),
        })),
    };

    // Campos opcionais — só incluídos se preenchidos
    if (formValues.notes?.trim()) payload.notes = formValues.notes.trim();
    if (formValues.starts_date) payload.starts_date = formValues.starts_date;
    if (formValues.ends_date) payload.ends_date = formValues.ends_date;
    if (planTargets?.kcal) payload.kcal_target = planTargets.kcal;
    if (planTargets?.protein_g)
      payload.protein_target_g = planTargets.protein_g;
    if (planTargets?.carbs_g) payload.carbs_target_g = planTargets.carbs_g;
    if (planTargets?.fats_g) payload.fats_target_g = planTargets.fats_g;

    try {
      await createMealPlan(payload);
      toast.success('Plano alimentar criado com sucesso!');
      setShowArchived(false); // volta a mostrar apenas ativos para destacar o novo plano
      await reloadPlans(false);
      setView('list');
      setPlanTargets(null);
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Erro ao criar plano alimentar.'
      );
    }
  };

  // Arquivo de plano: pede confirmação via AlertDialog
  const handleArchiveConfirm = async () => {
    if (!archiveTarget) return;
    try {
      await archiveMealPlan(archiveTarget.id);
      toast.success(`Plano "${archiveTarget.name}" arquivado.`);
      // Remove da lista local sem re-fetch
      setPlans((prev) => prev.filter((plan) => plan.id !== archiveTarget.id));
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Erro ao arquivar plano alimentar.'
      );
    } finally {
      setArchiveTarget(null);
    }
  };

  // Handler para reativar plano arquivado
  const handleUnarchiveConfirm = async () => {
    if (!unarchiveTarget) return;
    try {
      await unarchiveMealPlan(unarchiveTarget.id);
      toast.success(`Plano "${unarchiveTarget.name}" reativado.`);
      setPlans((prev) => prev.filter((plan) => plan.id !== unarchiveTarget.id)); // remove da lista atual
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Erro ao reativar plano alimentar.'
      );
    } finally {
      setUnarchiveTarget(null);
    }
  };

  // Cliente atualmente selecionado (objeto completo)
  const selectedClient = clients.find((c) => c.id === selectedClientId);

  // Render: Editor de plano existente

  if (view === 'editor' && editingPlan) {
    return (
      <div className="p-4 lg:p-6 space-y-6">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => {
              setView('list');
              setEditingPlan(null);
            }}
            className="p-2 rounded-lg text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <h1 className="text-lg font-semibold text-foreground">
              Editar Refeições
            </h1>
            <p className="text-sm text-muted-foreground">{editingPlan.name}</p>
          </div>
        </div>

        <div className="space-y-6 max-w-3xl">
          {/* Refeições — reutiliza exactamente o mesmo MealBuilder do builder */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-medium text-foreground">
                Refeições ({meals.length})
              </h2>
              <button
                type="button"
                onClick={handleAddMeal}
                className="text-sm text-primary hover:underline flex items-center gap-1"
              >
                <Plus className="h-3.5 w-3.5" /> Adicionar Refeição
              </button>
            </div>
            {meals.map((meal) => (
              <MealBuilder
                key={meal.localId}
                meal={meal}
                foods={foods}
                onUpdate={handleUpdateMeal}
                onRemove={() => handleRemoveMeal(meal.localId)}
                isOnly={meals.length === 1}
                clientSupplements={clientSupplements}
              />
            ))}
          </div>

          {/* Totais atualizados em tempo real */}
          {planMacros.kcal > 0 && (
            <Card className="bg-card border-border">
              <CardContent className="p-4 space-y-2">
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                  Total Diário
                </p>
                <MacroBar macros={planMacros} />
              </CardContent>
            </Card>
          )}

          <div className="flex gap-3">
            <Button onClick={handleSaveEdit} disabled={isSubmitting}>
              {isSubmitting ? 'A guardar...' : 'Guardar Alterações'}
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                setView('list');
                setEditingPlan(null);
              }}
              disabled={isSubmitting}
            >
              Cancelar
            </Button>
          </div>
        </div>
      </div>
    );
  }

  // Render: Builder
  if (view === 'builder') {
    return (
      <div className="p-4 lg:p-6 space-y-6">
        {/* Cabeçalho do builder */}
        <div className="flex items-center gap-3">
          <Button
            variant="ghost"
            size="icon"
            onClick={handleCancelCreate}
            className="text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-lg font-semibold text-foreground">
              Novo Plano Alimentar
            </h1>
            {selectedClient && (
              <p className="text-sm text-muted-foreground">
                {selectedClient.full_name}
              </p>
            )}
          </div>
        </div>

        <div className="space-y-6 max-w-3xl">
          {(planMacros.kcal > 0 || planTargets) && (
            <MacroProgressCard
              macros={planMacros}
              targets={planTargets || []}
            />
          )}

          {/* Badge a indicar que os targets vieram da calculadora */}
          {planTargets && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground bg-primary/5 border border-primary/20 rounded-lg px-3 py-2">
              <Calculator className="h-3.5 w-3.5 text-primary shrink-0" />
              <span>
                Objetivo de macros -{' '}
                <strong className="text-foreground">
                  {Math.round(planTargets.kcal)} kcal
                </strong>
              </span>
            </div>
          )}

          {/* Detalhes do plano */}
          <Card className="bg-card border-border">
            <CardContent className="p-5 space-y-4">
              <h2 className="text-sm font-medium text-foreground">
                Detalhes do Plano
              </h2>

              {/* Nome do plano — campo obrigatório */}
              <div className="space-y-1.5">
                <Label htmlFor="plan-name">
                  Nome do Plano <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="plan-name"
                  placeholder="Ex: Plano de Manutenção — Semana 1"
                  {...register('name', {
                    required: 'O nome do plano é obrigatório.',
                  })}
                />
                {errors.name && (
                  <p className="text-xs text-destructive">
                    {errors.name.message}
                  </p>
                )}
              </div>

              {/* Datas opcionais */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label htmlFor="starts-date">Data de Início (opcional)</Label>
                  <Input
                    id="starts-date"
                    type="date"
                    {...register('starts_date')}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="ends-date">Data de Fim (opcional)</Label>
                  <Input
                    id="ends-date"
                    type="date"
                    {...register('ends_date')}
                  />
                </div>
              </div>

              {/* Notas */}
              <div className="space-y-1.5">
                <Label htmlFor="plan-notes">Notas (opcional)</Label>
                <Textarea
                  id="plan-notes"
                  placeholder="Instruções gerais, observações sobre o plano..."
                  rows={3}
                  {...register('notes')}
                />
              </div>
            </CardContent>
          </Card>

          {/* Refeições */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-medium text-foreground">
                Refeições ({meals.length})
              </h2>
              <Button variant="outline" size="sm" onClick={handleAddMeal}>
                <Plus className="h-3.5 w-3.5 mr-1.5" />
                Adicionar Refeição
              </Button>
            </div>

            {meals.map((meal) => (
              <MealBuilder
                key={meal.localId}
                meal={meal}
                foods={foods}
                onUpdate={handleUpdateMeal}
                onRemove={() => handleRemoveMeal(meal.localId)}
                isOnly={meals.length === 1}
                clientSupplements={clientSupplements}
              />
            ))}
          </div>

          {/* Acções */}
          <div className="flex gap-3">
            <Button
              onClick={handleSubmit(onSubmit)}
              disabled={isSubmitting}
              className="flex-1 sm:flex-none"
            >
              {isSubmitting ? 'A guardar...' : 'Guardar Plano'}
            </Button>
            <Button
              variant="outline"
              onClick={handleCancelCreate}
              disabled={isSubmitting}
            >
              Cancelar
            </Button>
          </div>
        </div>
      </div>
    );
  }

  // Render: Lista de planos
  return (
    <div className="p-4 lg:p-6 space-y-6">
      {/* Título da página */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <UtensilsCrossed className="h-5 w-5 text-muted-foreground" />
          <h1 className="text-lg font-semibold text-foreground">
            Planos Alimentares
          </h1>
        </div>

        {/* Botão "Novo Plano" → Redireciona para calculadora */}
        {selectedClientId && (
          <Button onClick={handleStartCreate} size="sm">
            <Calculator className="h-4 w-4 mr-1.5" />
            Novo Plano
          </Button>
        )}
      </div>

      {/* Seletor de cliente */}
      <Card className="bg-card border-border">
        <CardContent className="p-4">
          <Label
            htmlFor="client-select"
            className="text-sm font-medium mb-2 block"
          >
            Cliente
          </Label>
          {loadingClients ? (
            <div className="h-9 rounded-md bg-muted animate-pulse" />
          ) : (
            <select
              id="client-select"
              className="w-full h-9 rounded-md border border-input bg-background px-3 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              value={selectedClientId}
              onChange={(e) => {
                setSelectedClientId(e.target.value);
                setShowArchived(false); // reset do filtro de arquivados ao mudar de cliente
              }}
            >
              <option value="">Seleciona um cliente...</option>
              {clients.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.full_name}
                </option>
              ))}
            </select>
          )}
        </CardContent>
      </Card>

      {/* Área de planos */}
      {!selectedClientId ? (
        <div className="text-center py-16 text-muted-foreground">
          <UtensilsCrossed className="h-10 w-10 mx-auto mb-3 opacity-30" />
          <p className="text-sm">
            Seleciona um cliente para ver os seus planos alimentares.
          </p>
        </div>
      ) : loadingPlans ? (
        <div className="space-y-3">
          {[1, 2].map((i) => (
            <div key={i} className="h-28 rounded-xl bg-card animate-pulse" />
          ))}
        </div>
      ) : (
        <>
          {/* ================================================================
              [NR-07] NOVO — Toggle Ativos / Arquivados.
              
              Dois botões em estilo "tab" que alternam o valor de showArchived.
              O botão ativo recebe bg-background + shadow para destacar.
              
              Porquê não usar o componente Tabs do shadcn?
              Tabs usa state interno com strings — aqui o state já é um boolean
              (showArchived) que mapeia diretamente para o parâmetro da API.
              Usar botões simples é mais direto e sem overhead.
              ================================================================ */}
          <div className="flex items-center gap-1 p-1 bg-secondary rounded-lg w-fit">
            <button
              type="button"
              onClick={() => setShowArchived(false)}
              className={`px-3 py-1.5 text-sm rounded-md transition-colors ${
                !showArchived
                  ? 'bg-background text-foreground shadow-sm font-medium'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              Ativos
            </button>
            <button
              type="button"
              onClick={() => setShowArchived(true)}
              className={`px-3 py-1.5 text-sm rounded-md transition-colors flex items-center gap-1.5 ${
                showArchived
                  ? 'bg-background text-foreground shadow-sm font-medium'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              <Archive className="h-3.5 w-3.5" />
              Arquivados
            </button>
          </div>

          {visiblePlans.length === 0 ? (
            <div className="text-center py-16 text-muted-foreground">
              <UtensilsCrossed className="h-10 w-10 mx-auto mb-3 opacity-30" />
              <p className="text-sm mb-4">
                {showArchived
                  ? `${selectedClient?.full_name} não tem planos arquivados.`
                  : `${selectedClient?.full_name} ainda não tem planos alimentares.`}
              </p>
              {/* Botão "Criar" só aparece na tab Ativos */}
              {!showArchived && (
                <Button onClick={handleStartCreate} size="sm">
                  <Calculator className="h-4 w-4 mr-1.5" />
                  Criar Primeiro Plano
                </Button>
              )}
            </div>
          ) : (
            <div className="space-y-3">
              {visiblePlans.map((plan) => (
                <Card key={plan.id} className="bg-card border-border">
                  <CardContent className="p-5">
                    <div className="flex flex-col sm:flex-row sm:items-start gap-3">
                      <div className="flex-1 space-y-2">
                        <div className="flex items-center gap-2 flex-wrap">
                          <h3 className="text-sm font-semibold text-foreground">
                            {plan.name}
                          </h3>

                          {/* Badge Ativo — só planos não arquivados */}
                          {plan.active && !plan.archived_at && (
                            <Badge className="bg-green-500/10 text-green-600 dark:text-green-400 border-0 text-xs gap-1">
                              <CheckCircle2 className="h-3 w-3" />
                              Ativo
                            </Badge>
                          )}

                          {/* [NR-07] Badge Arquivado — só na tab de arquivados */}
                          {plan.archived_at && (
                            <Badge
                              variant="outline"
                              className="text-xs text-muted-foreground"
                            >
                              <Archive className="h-3 w-3 mr-1" />
                              Arquivado {formatDate(plan.archived_at)}
                            </Badge>
                          )}
                        </div>

                        {(plan.starts_date || plan.ends_date) && (
                          <p className="text-xs text-muted-foreground">
                            {plan.starts_date && formatDate(plan.starts_date)}
                            {plan.starts_date && plan.ends_date && ' → '}
                            {plan.ends_date && formatDate(plan.ends_date)}
                          </p>
                        )}

                        <p className="text-xs text-muted-foreground">
                          {plan.meals.length} refeição
                          {plan.meals.length !== 1 ? 'ões' : ''}
                          {' · '}
                          {plan.meals.reduce(
                            (acc, m) => acc + m.items.length,
                            0
                          )}{' '}
                          alimentos
                        </p>

                        {plan.plan_macros && (
                          <MacroBar macros={plan.plan_macros} compact />
                        )}

                        {plan.notes && (
                          <p className="text-xs text-muted-foreground italic">
                            {plan.notes}
                          </p>
                        )}
                      </div>
                      <div className="flex gap-1 self-start sm:self-center shrink-0">
                        {!plan.archived_at ? (
                          // Plano ativo: Editar + Arquivar
                          <>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-muted-foreground hover:text-primary"
                              onClick={() => handleStartEdit(plan)}
                              title="Editar refeições"
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-muted-foreground hover:text-destructive"
                              onClick={() => setArchiveTarget(plan)}
                              title="Arquivar plano"
                            >
                              <Archive className="h-4 w-4" />
                            </Button>
                          </>
                        ) : (
                          // Plano arquivado: Reativar
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8 text-muted-foreground hover:text-primary"
                            onClick={() => setUnarchiveTarget(plan)}
                            title="Reativar plano"
                          >
                            <RotateCcw className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </>
      )}

      {/* Dialog: confirmar arquivamento */}
      <AlertDialog
        open={!!archiveTarget}
        onOpenChange={(open) => !open && setArchiveTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Arquivar Plano</AlertDialogTitle>
            <AlertDialogDescription>
              Tens a certeza que queres arquivar o plano{' '}
              <strong>"{archiveTarget?.name}"</strong>?<br />O plano não será
              apagado. Podes reativá-lo mais tarde na tab Arquivados.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleArchiveConfirm}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Arquivar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Dialog: confirmar reativação de plano arquivado */}
      <AlertDialog
        open={!!unarchiveTarget}
        onOpenChange={(open) => !open && setUnarchiveTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Reativar Plano</AlertDialogTitle>
            <AlertDialogDescription>
              O plano <strong>"{unarchiveTarget?.name}"</strong> vai ser
              reativado e voltará a aparecer na lista de planos ativos.
              <br />
              Os outros planos ativos não são afetados.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction onClick={handleUnarchiveConfirm}>
              Reativar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
