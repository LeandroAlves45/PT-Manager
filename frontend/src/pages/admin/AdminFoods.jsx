/**
 * AdminFoods.jsx — gestão do catálogo global de alimentos.
 *
 * Macros por 100g: carbs, protein, fats.
 * Kcal é calculado automaticamente pelo PostgreSQL (coluna GENERATED).
 * No frontend mostramos o valor calculado como referência.
 */

import { useEffect, useState, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import { Plus, Pencil, Trash2, Search, Apple, X } from 'lucide-react';
import {
  getGlobalFoods,
  createGlobalFood,
  updateGlobalFood,
  deleteGlobalFood,
} from '../../api/adminApi';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardContent } from '../../components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../components/ui/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '../../components/ui/alert-dialog';

// Calcula kcal no frontend para exibir como referência (100g)
function calcKcal(carbs, protein, fats) {
    const c = parseFloat(carbs) || 0;
    const p = parseFloat(protein) || 0;
    const f = parseFloat(fats) || 0;
    return Math.round(c * 4 + p * 4 + f * 9);
}

export default function AdminFoods() {
    const [foods, setFoods] = useState([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [dialogOpen, setDialogOpen] = useState(false);
    const [editing, setEditing] = useState(null);
    const [deleteTarget, setDeleteTarget] = useState(null);
    const [saving, setSaving] = useState(false);

    // Valores vigiados para calcular kcal em tempo real
    const { register, handleSubmit, watch, reset, formState: { errors } } = useForm();
    const watchedCarbs = watch('carbs', 0);
    const watchedProtein = watch('protein', 0);
    const watchedFats = watch('fats', 0);
    const previewKcal = calcKcal(watchedCarbs, watchedProtein, watchedFats);

    // Dados 

    const fetchFoods = useCallback(async () => {
        try {
            setLoading(true);
            const data = await getGlobalFoods();
            setFoods(data.sort((a, b) => a.name.localeCompare(b.name, 'pt')));
        } catch {
            toast.error('Erro ao carregar alimentos.');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        fetchFoods();
    }, [fetchFoods]);

    const filtered = foods.filter(f => f.name.toLowerCase().includes(search.toLowerCase()));

    // Dialog de criação/edição

    function openCreate() {
        setEditing(null);
        reset({ name: '', carbs: '', protein: '', fats: '' });
        setDialogOpen(true);
    }

    function openEdit(food) {
        setEditing(food);
        reset({ name: food.name, carbs: food.carbs, protein: food.protein, fats: food.fats });
        setDialogOpen(true);
    }

    // Guardar 

    async function onSubmit(data) {
        setSaving(true);
        try {
            const payload = {
                name: data.name.trim(),
                carbs: parseFloat(data.carbs),
                protein: parseFloat(data.protein),
                fats: parseFloat(data.fats),
            };
            if (editing) {
                await updateGlobalFood(editing.id, payload);
                toast.success('Alimento atualizado.');
            } else {
                await createGlobalFood(payload);
                toast.success('Alimento criado.');
            }
            setDialogOpen(false);
            fetchFoods();
        } catch (err){
            toast.error(err.response?.data?.detail || 'Erro ao guardar alimento.');
        } finally {
            setSaving(false);
        }
    }

    async function confirmDelete() {
            try {
                await deleteGlobalFood(deleteTarget.id);
                toast.success('Alimento eliminado.');
                setDeleteTarget(null);
                fetchFoods();
            } catch (err) {
                toast.error(err.response?.data?.detail || 'Erro ao eliminar alimento.');
            }
        }

        return (
        <div className="p-4 lg:p-6 flex flex-col gap-6">
    
        {/* Cabeçalho */}
        <div className="flex items-center justify-between gap-4">
            <div>
            <h1 className="text-2xl font-semibold text-foreground">Alimentos</h1>
            <p className="text-sm text-muted-foreground mt-1">
                Catálogo global — macros por 100g
            </p>
            </div>
            <Button onClick={openCreate} className="shrink-0">
            <Plus className="h-4 w-4 mr-2" />
            Novo Alimento
            </Button>
        </div>
    
        {/* Pesquisa */}
        <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
            placeholder="Pesquisar alimento..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-9"
            />
            {search && (
            <button
                onClick={() => setSearch('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            >
                <X className="h-4 w-4" />
            </button>
            )}
        </div>
    
        <p className="text-sm text-muted-foreground -mt-2">
            {filtered.length} alimento{filtered.length !== 1 ? 's' : ''}
            {search ? ` (filtrado de ${foods.length})` : ' no catálogo'}
        </p>
    
        {/* Lista */}
        {loading ? (
            <div className="flex justify-center py-12">
            <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
            </div>
        ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center gap-3 py-16 text-center">
            <Apple className="h-10 w-10 text-muted-foreground/30" />
            <p className="text-muted-foreground text-sm">
                {search ? 'Nenhum alimento corresponde à pesquisa.' : 'Nenhum alimento no catálogo.'}
            </p>
            </div>
        ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {filtered.map(food => (
                <Card key={food.id} className="border-border bg-card">
                <CardContent className="p-4">
                    <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                        <p className="font-medium text-sm text-foreground truncate">
                        {food.name}
                        </p>
                        {/* Macros em linha — formato compacto */}
                        <div className="flex gap-3 mt-1.5">
                        <span className="text-xs text-muted-foreground">
                            <span className="font-medium text-orange-400">HC</span> {food.carbs}g
                        </span>
                        <span className="text-xs text-muted-foreground">
                            <span className="font-medium text-blue-400">P</span> {food.protein}g
                        </span>
                        <span className="text-xs text-muted-foreground">
                            <span className="font-medium text-yellow-400">G</span> {food.fats}g
                        </span>
                        <span className="text-xs text-muted-foreground">
                            <span className="font-medium text-foreground">
                            {food.kcal ? Math.round(food.kcal) : calcKcal(food.carbs, food.protein, food.fats)}
                            </span> kcal
                        </span>
                        </div>
                    </div>
                    <div className="flex gap-1 shrink-0">
                        <button
                        onClick={() => openEdit(food)}
                        className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-foreground transition-colors"
                        >
                        <Pencil className="h-3.5 w-3.5" />
                        </button>
                        <button
                        onClick={() => setDeleteTarget(food)}
                        className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-destructive transition-colors"
                        >
                        <Trash2 className="h-3.5 w-3.5" />
                        </button>
                    </div>
                    </div>
                </CardContent>
                </Card>
            ))}
            </div>
        )}
    
        {/* Dialog criar/editar */}
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
            <DialogContent className="sm:max-w-md">
            <DialogHeader>
                <DialogTitle>{editing ? 'Editar Alimento' : 'Novo Alimento'}</DialogTitle>
            </DialogHeader>
            <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 pt-2">
                <div className="flex flex-col gap-1.5">
                <Label htmlFor="fname">Nome *</Label>
                <Input
                    id="fname"
                    placeholder="ex: Frango (Peito, cozido)"
                    {...register('name', { required: 'Nome é obrigatório' })}
                />
                {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
                </div>
    
                {/* Macros em grelha 3 colunas */}
                <div className="grid grid-cols-3 gap-3">
                {[
                    { id: 'carbs',   label: 'Hidratos (g)',  color: 'text-orange-400' },
                    { id: 'protein', label: 'Proteína (g)',  color: 'text-blue-400'   },
                    { id: 'fats',    label: 'Gordura (g)',   color: 'text-yellow-400' },
                ].map(({ id, label, color }) => (
                    <div key={id} className="flex flex-col gap-1.5">
                    <Label htmlFor={id} className={`text-xs ${color}`}>{label}</Label>
                    <Input
                        id={id}
                        type="number"
                        step="0.1"
                        min="0"
                        max="100"
                        placeholder="0"
                        {...register(id, {
                        required: 'Obrigatório',
                        min: { value: 0, message: '≥ 0' },
                        max: { value: 100, message: '≤ 100' },
                        })}
                    />
                    {errors[id] && <p className="text-xs text-destructive">{errors[id].message}</p>}
                    </div>
                ))}
                </div>
    
                {/* Preview kcal calculado */}
                <div className="rounded-md bg-muted/50 px-3 py-2 text-center">
                <p className="text-sm text-muted-foreground">
                    Estimativa: <span className="font-semibold text-foreground">{previewKcal} kcal</span> / 100g
                </p>
                </div>
    
                <DialogFooter className="pt-2">
                <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                    Cancelar
                </Button>
                <Button type="submit" disabled={saving}>
                    {saving ? 'A guardar...' : editing ? 'Guardar alterações' : 'Criar alimento'}
                </Button>
                </DialogFooter>
            </form>
            </DialogContent>
        </Dialog>
    
        {/* Confirmação de eliminação */}
        <AlertDialog open={!!deleteTarget} onOpenChange={() => setDeleteTarget(null)}>
            <AlertDialogContent>
            <AlertDialogHeader>
                <AlertDialogTitle>Eliminar alimento?</AlertDialogTitle>
                <AlertDialogDescription>
                Tens a certeza que queres eliminar <strong>{deleteTarget?.name}</strong>?
                Planos alimentares que contenham este alimento podem ser afectados.
                </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
                <AlertDialogCancel>Cancelar</AlertDialogCancel>
                <AlertDialogAction
                onClick={confirmDelete}
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                >
                Eliminar
                </AlertDialogAction>
            </AlertDialogFooter>
            </AlertDialogContent>
        </AlertDialog>
    
        </div>
    );
}
