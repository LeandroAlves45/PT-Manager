/**
 * Exercises.jsx — gestão do catálogo de exercícios do Personal Trainer.
 *
 * Regras de negócio aplicadas nesta página:
 *   - Exercícios com owner_trainer_id = null são GLOBAIS (criados pelo superuser).
 *     O Personal Trainer pode visualizá-los mas NÃO pode editá-los ou apagá-los.
 *   - Exercícios com owner_trainer_id = <uuid> são PRIVADOS do Personal Trainer.
 *     O Personal Trainer pode criar, editar e apagar os seus.
 *
 * O campo owner_trainer_id é devolvido pelo backend desde a correcção do schema
 * ExerciseRead. Sem este campo, seria impossível distinguir globais de privados.
 */

import { useState, useEffect, useMemo } from 'react';
import { matchesSearch } from '@/utils/validators';
import MuscleMultiSelect from '@/components/exercises/MuscleMultiSelect';
import { useForm, Controller } from 'react-hook-form';
import { toast } from 'react-toastify';
import {
  getExercises,
  createExercise,
  updateExercise,
  deleteExercise,
} from '@/api/exercisesApi';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
import {
  MoreHorizontal,
  Plus,
  Edit,
  Search,
  ExternalLink,
  Trash2,
  Globe,
  Lock,
} from 'lucide-react';
import { cn } from '@/lib/utils';

export default function Exercises() {
  const [exercises, setExercises] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [sourceFilter, setSourceFilter] = useState('all');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingExercise, setEditingExercise] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: { name: '', muscles: '', url: '', is_active: true },
  });

  const fetchExercises = async () => {
    setLoading(true);
    try {
      const data = await getExercises();
      setExercises(data);
    } catch (error) {
      toast.error('Erro ao carregar exercícios');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchExercises();
  }, []);

  // Helpers - determinar se um exercício é global ou privado
  const isGlobal = (exercise) =>
    exercise.owner_trainer_id === null ||
    exercise.owner_trainer_id === undefined;

  //Filtragem local
  const filtered = useMemo(() => {
    let result = exercises;

    if (statusFilter === 'active') result = result.filter((e) => e.is_active);
    else if (statusFilter === 'inactive')
      result = result.filter((e) => !e.is_active);

    if (sourceFilter === 'global') result = result.filter((e) => isGlobal(e));
    else if (sourceFilter === 'private')
      result = result.filter((e) => !isGlobal(e));

    if (search.trim()) {
      const q = search.toLowerCase();
      result = result.filter((e) => matchesSearch(q, e.name, e.muscles));
    }
    return result;
  }, [exercises, search, statusFilter, sourceFilter]);

  const openAdd = () => {
    setEditingExercise(null);
    reset({ name: '', muscles: '', url: '', is_active: true });
    setDialogOpen(true);
  };

  const openEdit = (exercise) => {
    if (isGlobal(exercise)) {
      toast.info('Exercícios globais não podem ser editados nesta página.');
      return;
    }
    setEditingExercise(exercise);
    reset({
      name: exercise.name,
      muscles: exercise.muscles,
      url: exercise.url || '',
      is_active: exercise.is_active,
    });
    setDialogOpen(true);
  };

  const onSubmit = async (data) => {
    const cleaned = { ...data, url: data.url?.trim() || null };
    try {
      if (editingExercise) {
        await updateExercise(editingExercise.id, cleaned);
        toast.success('Exercício atualizado com sucesso!');
      } else {
        await createExercise(cleaned);
        toast.success('Exercício criado com sucesso!');
      }
      setDialogOpen(false);
      fetchExercises();
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Ocorreu um erro ao salvar o exercício.'
      );
    }
  };

  const handleDelete = async (exercise) => {
    if (!deleteTarget) return;
    try {
      await deleteExercise(deleteTarget.id);
      toast.success(`${deleteTarget.name} apagado.`);
      setDeleteTarget(null);
      fetchExercises();
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Ocorreu um erro ao apagar o exercício.'
      );
    }
  };

  // Contadores para os tabs
  const totalGlobal = exercises.filter((e) => isGlobal(e)).length;
  const totalPrivate = exercises.filter((e) => !isGlobal(e)).length;

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Cabeçalho */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Exercícios</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Gerir a biblioteca de exercícios
          </p>
        </div>
        <Button
          onClick={openAdd}
          className="bg-primary text-primary-foreground hover:bg-primary/90"
        >
          <Plus className="w-4 h-4 mr-1.5" />
          Novo Exercício
        </Button>
      </div>

      {/* Filtros */}
      <div className="flex flex-col sm:flex-row gap-3 flex-wrap">
        {/* Pesquisa */}
        <div className="relative w-full sm:w-72">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Pesquisar exercícios..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9 bg-background border-input text-foreground"
          />
        </div>

        {/* Filtro por estado */}
        <Tabs value={statusFilter} onValueChange={setStatusFilter}>
          <TabsList className="bg-secondary">
            <TabsTrigger
              value="all"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Todos {exercises.length}
            </TabsTrigger>
            <TabsTrigger
              value="active"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Ativos
            </TabsTrigger>
            <TabsTrigger
              value="inactive"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Inativos
            </TabsTrigger>
          </TabsList>
        </Tabs>

        {/* Filtro por fonte - global ou privado */}
        <Tabs value={sourceFilter} onValueChange={setSourceFilter}>
          <TabsList className="bg-secondary">
            <TabsTrigger
              value="all"
              className="data-[state=active]:bg-background"
            >
              Todos
            </TabsTrigger>
            <TabsTrigger
              value="global"
              className="data-[state=active]:bg-background"
            >
              <Globe className="h-3.5 w-3.5 mr-1" /> Globais {totalGlobal}
            </TabsTrigger>
            <TabsTrigger
              value="private"
              className="data-[state=active]:bg-background "
            >
              <Lock className="h-3.5 w-3.5 mr-1" /> Meus {totalPrivate}
            </TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {/* Tabela */}
      {loading ? (
        <div className="space-y-3">
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className="h-14 rounded-lg bg-card border border-border animate-pulse"
            />
          ))}
        </div>
      ) : (
        <div className="rounded-lg border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="border-border hover:bg-transparent">
                <TableHead className="text-muted-foreground">Nome</TableHead>
                <TableHead className="text-muted-foreground hidden sm:table-cell">
                  Músculos
                </TableHead>
                <TableHead className="text-muted-foreground hidden md:table-cell">
                  Vídeo
                </TableHead>
                <TableHead className="text-muted-foreground">Estado</TableHead>
                <TableHead className="text-muted-foreground text-right">
                  Ações
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={5}
                    className="text-center py-8 text-muted-foreground"
                  >
                    Nenhum exercício encontrado
                  </TableCell>
                </TableRow>
              ) : (
                filtered.map((exercise) => {
                  const global = isGlobal(exercise);
                  return (
                    <TableRow
                      key={exercise.id}
                      className="border-border hover:bg-accent/50"
                    >
                      <TableCell>
                        <span className="text-sm font-medium text-foreground">
                          {exercise.name}
                        </span>
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">
                        <div className="flex flex-wrap gap-1">
                          {exercise.muscles.split(',').map((muscle) => (
                            <Badge
                              key={muscle.trim()}
                              variant="outline"
                              className="text-xs border-border"
                            >
                              {muscle.trim()}
                            </Badge>
                          ))}
                        </div>
                      </TableCell>
                      <TableCell className="hidden md:table-cell">
                        {exercise.url ? (
                          <a
                            href={exercise.url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-sm text-primary hover:underline flex items-center gap-1"
                          >
                            <ExternalLink className="h-3.5 w-3.5" /> Ver
                          </a>
                        ) : (
                          <span className="text-sm text-muted-foreground">
                            —
                          </span>
                        )}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant="outline"
                          className={cn(
                            'text-xs',
                            exercise.is_active
                              ? 'bg-success/15 text-success border-success/20'
                              : 'bg-muted text-muted-foreground border-border'
                          )}
                        >
                          {exercise.is_active ? 'Ativo' : 'Inativo'}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        {global ? (
                          // Exercícios globais — sem acções disponíveis para o Personal Trainer
                          <span className="text-xs text-muted-foreground pr-2">
                            —
                          </span>
                        ) : (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                variant="ghost"
                                size="icon"
                                className="h-8 w-8 text-muted-foreground hover:text-foreground"
                              >
                                <MoreHorizontal className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent
                              align="end"
                              className="bg-popover border-border"
                            >
                              <DropdownMenuItem
                                onClick={() => openEdit(exercise)}
                              >
                                <Edit className="h-4 w-4 mr-2" /> Editar
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem
                                className="text-destructive"
                                onClick={() => setDeleteTarget(exercise)}
                              >
                                <Trash2 className="h-4 w-4 mr-2" /> Apagar
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Dialog de criação/edição */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {editingExercise ? 'Editar Exercício' : 'Novo Exercício'}
            </DialogTitle>
            <DialogDescription className="text-muted-foreground">
              {editingExercise
                ? 'Atualiza os detalhes do exercício.'
                : 'Preenche os detalhes para criar um novo exercício.'}
            </DialogDescription>
          </DialogHeader>
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="flex flex-col gap-4 mt-2"
          >
            {/* Campo de nome */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="ex-name">Nome *</Label>
              <Input
                id="ex-name"
                {...register('name', { required: 'Nome obrigatório' })}
                className="bg-background border-input text-foreground"
                placeholder="Ex: Supino Plano"
              />
              {errors.name && (
                <span className="text-xs text-destructive">
                  {errors.name.message}
                </span>
              )}
            </div>

            {/* Campo de músculos - multi-select customizado */}
            <div className="flex flex-col gap-1.5">
              <Label>
                Músculos <span className="text-destructive">*</span>
              </Label>
              <Controller
                name="muscles"
                control={control}
                rules={{ required: 'Seleciona pelo menos um músculo' }}
                render={({ field }) => (
                  <MuscleMultiSelect
                    value={field.value}
                    onChange={field.onChange}
                    error={errors.muscles?.message}
                  />
                )}
              />
            </div>

            {/* Campo de URL */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="ex-url">URL do Vídeo</Label>
              <Input
                id="ex-url"
                {...register('url')}
                className="bg-background border-input text-foreground"
                placeholder="https://exemplo.com/exercicio"
              />
            </div>

            {/* Toggle de ativo/inativo */}
            <div className="flex items-center justify-between">
              <Label htmlFor="ex-active">Ativo</Label>
              <Switch
                id="ex-active"
                checked={watch('is_active')}
                onCheckedChange={(checked) => setValue('is_active', checked)}
              />
            </div>

            {/* Botões de ação */}
            <div className="flex justify-end gap-3 pt-2">
              <Button
                type="button"
                variant="ghost"
                onClick={() => setDialogOpen(false)}
                className="text-muted-foreground"
              >
                Cancelar
              </Button>
              <Button
                type="submit"
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                {isSubmitting
                  ? 'A guardar...'
                  : editingExercise
                    ? 'Guardar Alterações'
                    : 'Criar Exercício'}
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      {/* AlertDialog de confirmação de apagar */}
      <AlertDialog
        open={!!deleteTarget}
        onOpenChange={() => setDeleteTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Apagar exercício?</AlertDialogTitle>
            <AlertDialogDescription>
              Vais apagar permanentemente <strong>{deleteTarget?.name}</strong>.
              Esta ação não pode ser revertida.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={handleDelete}
            >
              Apagar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
