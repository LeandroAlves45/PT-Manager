/**
 * AdminExercises.jsx — gestão do catálogo global de exercícios.
 *
 * O admin pode criar, editar e eliminar exercícios globais
 * (owner_trainer_id = NULL), que são visíveis a todos os Personal Trainers.
 *
 * Exercícios privados de Personal Trainers não aparecem aqui — só os globais.
 */

import { useEffect, useState, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import { matchesSearch } from '@/utils/validators';
import {
  Plus,
  Pencil,
  Trash2,
  Search,
  Dumbbell,
  ExternalLink,
  X,
} from 'lucide-react';
import {
  getGlobalExercises,
  createGlobalExercise,
  updateGlobalExercise,
  deleteGlobalExercise,
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

export default function AdminExercises() {
  const [exercises, setExercises] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [saving, setSaving] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm();

  // Carrega exercícios globais

  const fetchExercises = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getGlobalExercises();
      // Ordena por nome
      setExercises(data.sort((a, b) => a.name.localeCompare(b.name, 'pt')));
    } catch {
      toast.error('Erro ao carregar exercícios.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchExercises();
  }, [fetchExercises]);

  // Filtro de pesquisa

  const filteredExercises = exercises.filter(
    (ex) => !search.trim() || matchesSearch(search, ex.name, ex.muscles)
  );

  // Abrir Dialog

  function openCreate() {
    setEditing(null);
    reset({ name: '', muscles: '', url: '' });
    setDialogOpen(true);
  }

  function openEdit(exercise) {
    setEditing(exercise);
    reset({
      name: exercise.name,
      muscles: exercise.muscles,
      url: exercise.url || '',
    });
    setDialogOpen(true);
  }

  // Guardar (criar ou editar)

  async function onSubmit(data) {
    setSaving(true);
    try {
      if (editing) {
        await updateGlobalExercise(editing.id, {
          name: data.name.trim(),
          muscles: data.muscles.trim(),
          url: data.url.trim() || null,
        });
        toast.success('Exercício atualizado.');
      } else {
        await createGlobalExercise({
          name: data.name.trim(),
          muscles: data.muscles.trim(),
          url: data.url.trim() || null,
        });
        toast.success('Exercício criado.');
      }
      setDialogOpen(false);
      fetchExercises();
    } catch (err) {
      toast.error(err.response?.data?.detail || 'Erro ao guardar exercício.');
    } finally {
      setSaving(false);
    }
  }

  // Eliminar

  async function confirmDelete() {
    try {
      await deleteGlobalExercise(deleteTarget.id);
      toast.success('Exercício eliminado.');
      setDeleteTarget(null);
      fetchExercises();
    } catch (err) {
      toast.error(err.response?.data?.detail || 'Erro ao eliminar exercício.');
    }
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Cabeçalho */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Exercícios</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Catálogo global — visível a todos os Personal Trainers.
          </p>
        </div>
        <Button onClick={openCreate} className="shrink-0">
          <Plus className="h-4 w-4 mr-2" />
          Novo Exercício
        </Button>
      </div>

      {/* Pesquisa */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Pesquisar por nome ou músculos..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
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

      {/* Contador */}
      <p className="text-sm text-muted-foreground -mt-2">
        {filteredExercises.length} exercício
        {filteredExercises.length !== 1 ? 's' : ''}
        {search ? ` (filtrado de ${exercises.length})` : 'no catálogo'}
      </p>

      {/* Lista de exercícios */}
      {loading ? (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
        </div>
      ) : filteredExercises.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-16 text-center">
          <Dumbbell className="h-10 w-10 text-muted-foreground/30" />
          <p className="text-muted-foreground text-sm">
            {search
              ? 'Nenhum exercício corresponde à pesquisa.'
              : 'Nenhum exercício no catálogo.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {filteredExercises.map((exercise) => (
            <Card key={exercise.id} className="border-border bg-card">
              <CardContent className="p-4">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-foreground truncate">
                      {exercise.name}
                    </p>
                    <p className="text-xs text-muted-foreground mt-0.5 line-clamp-2">
                      {exercise.muscles}
                    </p>
                  </div>
                  {/* Ações: Editar e Eliminar */}
                  <div className="flex gap-1 shrink-0">
                    {exercise.url && (
                      <a
                        href={exercise.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-primary transition-colors"
                        title="Ver vídeo"
                      >
                        <ExternalLink className="h-3.5 w-3.5" />
                      </a>
                    )}
                    <button
                      onClick={() => openEdit(exercise)}
                      className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-primary transition-colors"
                      title="Editar"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                    <button
                      onClick={() => openEdit(exercise)}
                      className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-foreground transition-colors"
                      title="Editar"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                    <button
                      onClick={() => setDeleteTarget(exercise)}
                      className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-destructive transition-colors"
                      title="Eliminar"
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

      {/* Dialog de criação/edição */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {editing ? 'Editar Exercício' : 'Novo Exercício'}
            </DialogTitle>
          </DialogHeader>
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="flex flex-col gap-4 pt-2"
          >
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="name">Nome</Label>
              <Input
                id="name"
                placeholder="ex: Agachamento"
                {...register('name', { required: 'O nome é obrigatório.' })}
              />
              {errors.name && (
                <p className="text-xs text-destructive">
                  {errors.name.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="muscles">Músculos</Label>
              <Input
                id="muscles"
                placeholder="ex: Quadríceps, Glúteos"
                {...register('muscles', {
                  required: 'Os músculos são obrigatórios.',
                })}
              />
              {errors.muscles && (
                <p className="text-xs text-destructive">
                  {errors.muscles.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="url">URL do Vídeo (opcional)</Label>
              <Input
                id="url"
                placeholder="ex: https://www.youtube.com/watch?v=..."
                {...register('url')}
              />
            </div>
            <DialogFooter className="pt-2">
              <Button
                type="submit"
                variant="outline"
                onClick={() => setDialogOpen(false)}
              >
                Cancelar
              </Button>
              <Button type="submit" disabled={saving}>
                {saving
                  ? 'A guardar...'
                  : editing
                    ? 'Guardar alterações'
                    : 'Criar Exercício'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Confirmação de eliminação */}
      <AlertDialog
        open={!!deleteTarget}
        onOpenChange={() => setDeleteTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Confirmar Eliminação?</AlertDialogTitle>
            <AlertDialogDescription>
              Tens a certeza que queres eliminar{' '}
              <strong>{deleteTarget?.name}</strong>? Esta ação não pode ser
              revertida. Planos de treino que usem este exercício podem ser
              afectados.
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
