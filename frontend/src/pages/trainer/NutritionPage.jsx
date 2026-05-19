/**
 * NutritionPage.jsx — gestão do catálogo de alimentos do Personal Trainer.
 *
 * Regras de negócio aplicadas:
 *   - Alimentos com owner_trainer_id = null são GLOBAIS — o Personal Trainer pode
 *     visualizá-los mas NÃO pode editá-los ou apagá-los.
 *   - Alimentos com owner_trainer_id = <uuid> são PRIVADOS do Personal Trainer.
 *
 * Funcionalidades:
 *   - Lista alimentos com pesquisa por nome e filtro por fonte (global/privado)
 *   - Cards com macros (HC / P / G) e kcal por 100g
 *   - Criar, editar e desativar alimentos privados
 *   - Badge "Global" para alimentos da plataforma
 */

import { useState, useEffect, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import { matchesSearch } from '@/utils/validators';
import { parseNullableFloat } from '@/utils/formatters';
import {
  getFoods,
  createFood,
  updateFood,
  deleteFood,
} from '@/api/nutritionApi';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
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
import { Plus, Search, Edit, Trash2, Utensils } from 'lucide-react';

export default function NutritionPage() {
  const [foods, setFoods] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [sourceFilter, setSourceFilter] = useState('all'); // 'all', 'global', 'private'

  // Dialog de criação/edição
  const [formOpen, setFormOpen] = useState(false);
  const [editingFood, setEditingFood] = useState(null); // null = criar novo

  // AlertDialog de confirmação de desactivação
  const [deleteTarget, setDeleteTarget] = useState(null); // alimento a desativar

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      name: '',
      carbs: '',
      protein: '',
      fats: '',
    },
  });

  // Carrega catálogo de alimentos
  const loadFoods = async () => {
    try {
      setLoading(true);
      const data = await getFoods(false); // carrega todos (ativos e inativos)
      setFoods(data);
    } catch {
      toast.error('Erro ao carregar alimentos. Tente novamente.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadFoods();
  }, []);

  // Um alimento é global quando owner_trainer_id = null ou undefined
  const isGlobal = (food) =>
    food.owner_trainer_id == null || food.owner_trainer_id === undefined;

  // Filtragem local, pesquisa por nome
  const filtered = useMemo(() => {
    return foods.filter((food) => {
      if (sourceFilter === 'global' && !isGlobal(food)) return false;
      if (sourceFilter === 'private' && isGlobal(food)) return false;
      if (search && !matchesSearch(search, food.name)) return false;
      return true;
    });
  }, [foods, search, sourceFilter]);

  const totalGlobal = foods.filter((food) => isGlobal(food)).length;
  const totalPrivate = foods.filter((food) => !isGlobal(food)).length;

  // Abre o dialog para criar um novo alimento
  const handleOpenCreate = () => {
    setEditingFood(null);
    reset({ name: '', carbs: '', protein: '', fats: '' });
    setFormOpen(true);
  };

  // Abre o dialog para editar um alimento pré-preenchido
  const handleOpenEdit = (food) => {
    if (isGlobal(food)) {
      toast.error('Alimentos globais não podem ser editados.');
      return;
    }
    setEditingFood(food);
    reset({
      name: food.name,
      carbs: String(food.carbs),
      protein: String(food.protein),
      fats: String(food.fats),
    });
    setFormOpen(true);
  };

  // Submete o formulário de criação ou edição
  const onSubmit = async (data) => {
    const payload = {
      name: data.name.trim(),
      carbs: parseNullableFloat(data.carbs) ?? 0,
      protein: parseNullableFloat(data.protein) ?? 0,
      fats: parseNullableFloat(data.fats) ?? 0,
    };

    try {
      if (editingFood) {
        await updateFood(editingFood.id, payload);
        toast.success('Alimento actualizado com sucesso.');
      } else {
        await createFood(payload);
        toast.success('Alimento criado com sucesso.');
      }
      setFormOpen(false);
      loadFoods();
    } catch (error) {
      toast.error(
        error.response?.data?.detail ||
          'Erro ao salvar alimento. Tente novamente.'
      );
    }
  };

  // Desativa um alimento após confirmação
  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteFood(deleteTarget.id);
      toast.success(`${deleteTarget.name} desativado.`);
      setDeleteTarget(null);
      loadFoods();
    } catch (error) {
      toast.error(
        error.response?.data?.detail ||
          'Erro ao desativar alimento. Tente novamente.'
      );
    }
  };

  // Formata kcal: (carbs * 4) + (protein * 4) + (fats * 9)
  const calcKcal = (food) => {
    if (food.kcal !== null && food.kcal !== undefined)
      return Math.round(food.kcal);
    return Math.round(
      (food.carbs || 0) * 4 + (food.protein || 0) * 4 + (food.fats || 0) * 9
    );
  };

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Cabeçalho */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Nutrição</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Catálogo de alimentos com macros por 100g
          </p>
        </div>
        <Button
          onClick={handleOpenCreate}
          className="bg-primary text-primary-foreground hover:bg-primary/90"
        >
          <Plus className="w-4 h-4 mr-1.5" />
          Novo Alimento
        </Button>
      </div>

      {/* Filtros */}
      <div className="flex flex-col sm:flex-row gap-3 flex-wrap">
        <div className="relative w-full sm:w-72">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Pesquisar alimentos..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9 bg-background border-input"
          />
        </div>

        <Tabs value={sourceFilter} onValueChange={setSourceFilter}>
          <TabsList className="bg-secondary">
            <TabsTrigger
              value="all"
              className="data-[state=active]:bg-background"
            >
              Todos {foods.length}
            </TabsTrigger>
            <TabsTrigger
              value="global"
              className="data-[state=active]:bg-background"
            >
              Globais {totalGlobal}
            </TabsTrigger>
            <TabsTrigger
              value="private"
              className="data-[state=active]:bg-background"
            >
              Meus {totalPrivate}
            </TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {/* Grelha de cards */}
      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
            <div
              key={i}
              className="h-36 rounded-lg bg-card border border-border animate-pulse"
            />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-muted-foreground text-center">
          <Utensils className="h-10 w-10 mb-3 opacity-30" />
          <p className="font-medium">Nenhum alimento encontrado.</p>
          <p className="text-sm mt-1">
            {sourceFilter === 'private'
              ? 'Cria o primeiro alimento do teu catálogo privado.'
              : 'Tenta ajustar a pesquisa.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {filtered.map((food) => {
            const global = isGlobal(food);
            return (
              <Card
                key={food.id}
                className={`border-border bg-card ${!food.is_active ? 'opacity-50' : ''}`}
              >
                <CardContent className="p-4">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      <p className="font-medium text-sm text-foreground truncate">
                        {food.name}
                      </p>
                      {/* Macros em linha — formato compacto */}
                      <div className="flex gap-3 mt-1.5">
                        <span className="text-xs text-muted-foreground">
                          <span className="font-medium text-orange-400">
                            HC
                          </span>{' '}
                          {food.carbs}g
                        </span>
                        <span className="text-xs text-muted-foreground">
                          <span className="font-medium text-blue-400">P</span>{' '}
                          {food.protein}g
                        </span>
                        <span className="text-xs text-muted-foreground">
                          <span className="font-medium text-yellow-400">G</span>{' '}
                          {food.fats}g
                        </span>
                        <span className="text-xs text-muted-foreground">
                          <span className="font-medium text-foreground">
                            {calcKcal(food)}
                          </span>{' '}
                          kcal
                        </span>
                      </div>
                    </div>
                    {/* Menu apenas para alimentos privados */}
                    {!global ? (
                      <div className="flex gap-1 shrink-0">
                        <button
                          onClick={() => handleOpenEdit(food)}
                          className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-foreground transition-colors"
                        >
                          <Edit className="h-3.5 w-3.5" />
                        </button>
                        <button
                          onClick={() => setDeleteTarget(food)}
                          className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-destructive transition-colors"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    ) : sourceFilter === 'global' ? (
                      <Badge variant="secondary" className="shrink-0 text-xs">
                        Global
                      </Badge>
                    ) : null}
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {/* Dialog: criar / editar */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="sm:max-w-md bg-card border-border text-foreground">
          <DialogHeader>
            <DialogTitle>
              {editingFood ? 'Editar Alimento' : 'Novo Alimento'}
            </DialogTitle>
            <DialogDescription className="text-muted-foreground">
              Valores por 100g. A kcal é calculada automaticamente.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 pt-2">
            <div className="space-y-1.5">
              <Label htmlFor="food-name">
                Nome <span className="text-destructive">*</span>
              </Label>
              <Input
                id="food-name"
                placeholder="Ex: Frango (Peito, cozido)"
                className="bg-background border-input"
                {...register('name', { required: 'Nome obrigatório' })}
              />
              {errors.name && (
                <p className="text-destructive text-xs">
                  {errors.name.message}
                </p>
              )}
            </div>

            <div className="grid grid-cols-3 gap-3">
              {[
                { id: 'carbs', label: 'HC (g)', color: 'text-blue-400' },
                {
                  id: 'protein',
                  label: 'Proteína (g)',
                  color: 'text-green-400',
                },
                { id: 'fats', label: 'Gordura (g)', color: 'text-orange-400' },
              ].map((field) => (
                <div key={field.id} className="space-y-1.5">
                  <Label htmlFor={`food-${field.id}`} className={field.color}>
                    {field.label} <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id={`food-${field.id}`}
                    type="number"
                    step="0.1"
                    min="0"
                    max="100"
                    placeholder="0"
                    className="bg-background border-input"
                    {...register(field.id, {
                      required: 'Obrigatório',
                      min: { value: 0, message: '≥ 0' },
                    })}
                  />
                  {errors[field.id] && (
                    <p className="text-destructive text-xs">
                      {errors[field.id].message}
                    </p>
                  )}
                </div>
              ))}
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <Button
                variant="outline"
                type="button"
                onClick={() => setFormOpen(false)}
              >
                Cancelar
              </Button>
              <Button
                onClick={handleSubmit(onSubmit)}
                disabled={isSubmitting}
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                {isSubmitting
                  ? 'A guardar...'
                  : editingFood
                    ? 'Guardar'
                    : 'Criar Alimento'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* AlertDialog: desativar */}
      <AlertDialog
        open={!!deleteTarget}
        onOpenChange={() => setDeleteTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Desativar alimento?</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{deleteTarget?.name}</strong> ficará inativo. Os planos
              alimentares existentes não são afetados.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={handleDelete}
            >
              Desativar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
