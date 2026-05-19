/**
 * AdminSupplements.jsx — gestão do catálogo global de suplementos.
 *
 * Suplementos globais: created_by_user_id = NULL.
 * O admin pode criar, editar, arquivar e eliminar.
 * Arquivar é preferível a eliminar — preserva histórico de atribuições.
 */

import { useEffect, useState, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import { Plus, Pencil, Trash2, Archive, Search, Pill, X } from 'lucide-react';
import {
  getGlobalSupplements,
  createGlobalSupplement,
  updateGlobalSupplement,
  archiveGlobalSupplement,
  deleteGlobalSupplement,
} from '../../api/adminApi';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Badge } from '../../components/ui/badge';
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

export default function AdminSupplements() {
  const [supplements, setSupplements] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [showArchived, setShowArchived] = useState(false);
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

  // Dados

  const fetchSupplements = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getGlobalSupplements();
      setSupplements(data.sort((a, b) => a.name.localeCompare(b.name, 'pt')));
    } catch {
      toast.error('Erro ao carregar suplementos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSupplements();
  }, [fetchSupplements]);

  // Aplica filtro de pesquisa e arquivados
  const filtered = supplements.filter((s) => {
    const matchSearch = s.name.toLowerCase().includes(search.toLowerCase());
    const matchArchived = showArchived ? true : !s.archived_at;
    return matchSearch && matchArchived;
  });

  const activeCount = supplements.filter((s) => !s.archived_at).length;
  const archivedCount = supplements.filter((s) => s.archived_at).length;

  // Dialog
  function openCreate() {
    setEditing(null);
    reset({
      name: '',
      description: '',
      serving_size: '',
      timing: '',
    });
    setDialogOpen(true);
  }

  // Guardar

  async function onSubmit(data) {
    setSaving(true);
    try {
      const payload = {
        name: data.name.trim(),
        description: data.description.trim() || null,
        serving_size: data.serving_size.trim() || null,
        timing: data.timing.trim() || null,
      };
      if (editing) {
        await updateGlobalSupplement(editing.id, payload);
        toast.success('Suplemento atualizado.');
      } else {
        await createGlobalSupplement(payload);
        toast.success('Suplemento criado.');
      }
      setDialogOpen(false);
      fetchSupplements();
    } catch (err) {
      toast.error(err.response?.data?.detail || 'Erro ao salvar suplemento.');
    } finally {
      setSaving(false);
    }
  }

  async function handleArchive(supplement) {
    try {
      await archiveGlobalSupplement(supplement.id);
      toast.success(` ${supplement.name} arquivado.`);
      fetchSupplements();
    } catch {
      toast.error('Erro ao arquivar suplemento.');
    }
  }

  async function confirmDelete() {
    try {
      await deleteGlobalSupplement(deleteTarget.id);
      toast.success('Suplemento eliminado.');
      setDeleteTarget(null);
      fetchSupplements();
    } catch {
      toast.error('Erro ao eliminar suplemento.');
    }
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Cabeçalho */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">
            Suplementos
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Catálogo global — {activeCount} ativos · {archivedCount} arquivados
          </p>
        </div>
        <Button onClick={openCreate} className="shrink-0">
          <Plus className="h-4 w-4 mr-2" />
          Novo Suplemento
        </Button>
      </div>

      {/* Pesquisa + toggle arquivados */}
      <div className="flex gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Pesquisar suplemento..."
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
        <Button
          variant={showArchived ? 'default' : 'outline'}
          onClick={() => setShowArchived((v) => !v)}
          className="shrink-0"
        >
          <Archive className="h-4 w-4 mr-2" />
          Arquivados
        </Button>
      </div>

      {/* Lista */}
      {loading ? (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-16 text-center">
          <Pill className="h-10 w-10 text-muted-foreground/30" />
          <p className="text-muted-foreground text-sm">
            {search
              ? 'Nenhum suplemento corresponde à pesquisa.'
              : 'Nenhum suplemento no catálogo.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {filtered.map((supplement) => (
            <Card
              key={supplement.id}
              className={`border-border bg-card ${supplement.archived_at ? 'opacity-60' : ''}`}
            >
              <CardContent className="p-4">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="font-medium text-sm text-foreground truncate">
                        {supplement.name}
                      </p>
                      {supplement.archived_at && (
                        <Badge variant="secondary" className="text-xs shrink-0">
                          Arquivado
                        </Badge>
                      )}
                    </div>
                    {supplement.serving_size && (
                      <p className="text-xs text-muted-foreground mt-0.5">
                        Dose: {supplement.serving_size}
                      </p>
                    )}
                    {supplement.timing && (
                      <p className="text-xs text-muted-foreground">
                        Timing: {supplement.timing}
                      </p>
                    )}
                  </div>
                  {/* Acções — só mostra para não arquivados */}
                  {!supplement.archived_at && (
                    <div className="flex gap-1 shrink-0">
                      <button
                        onClick={() => openEdit(supplement)}
                        className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-foreground transition-colors"
                        title="Editar"
                      >
                        <Pencil className="h-3.5 w-3.5" />
                      </button>
                      <button
                        onClick={() => handleArchive(supplement)}
                        className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-orange-500 transition-colors"
                        title="Arquivar"
                      >
                        <Archive className="h-3.5 w-3.5" />
                      </button>
                      <button
                        onClick={() => setDeleteTarget(supplement)}
                        className="h-7 w-7 flex items-center justify-center rounded text-muted-foreground hover:text-destructive transition-colors"
                        title="Eliminar"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  )}
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
            <DialogTitle>
              {editing ? 'Editar Suplemento' : 'Novo Suplemento'}
            </DialogTitle>
          </DialogHeader>
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="flex flex-col gap-4 pt-2"
          >
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="sname">Nome *</Label>
              <Input
                id="sname"
                placeholder="ex: Creatina Monohidratada"
                {...register('name', { required: 'Nome é obrigatório' })}
              />
              {errors.name && (
                <p className="text-xs text-destructive">
                  {errors.name.message}
                </p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="sdesc">Descrição</Label>
              <Input
                id="sdesc"
                placeholder="Breve descrição e benefícios"
                {...register('description')}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="sserving">Dose</Label>
                <Input
                  id="sserving"
                  placeholder="ex: 5g"
                  {...register('serving_size')}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="stiming">Timing</Label>
                <Input
                  id="stiming"
                  placeholder="ex: Pós-treino"
                  {...register('timing')}
                />
              </div>
            </div>
            <DialogFooter className="pt-2">
              <Button
                type="button"
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
                    : 'Criar suplemento'}
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
            <AlertDialogTitle>Eliminar suplemento?</AlertDialogTitle>
            <AlertDialogDescription>
              Tens a certeza que queres eliminar{' '}
              <strong>{deleteTarget?.name}</strong>? Considera arquivar em vez
              de eliminar para preservar o histórico de atribuições a clientes.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={confirmDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Eliminar mesmo assim
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
