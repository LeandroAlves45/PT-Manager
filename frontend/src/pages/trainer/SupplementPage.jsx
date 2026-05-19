/**
 * SupplementsPage.jsx — gestão do catálogo de suplementos (Personal Trainer).
 *
 * Regras de negócio aplicadas:
 *   - Suplementos com created_by_user_id = null são GLOBAIS.
 *     O Personal Trainer pode visualizá-los mas NÃO pode editar, arquivar ou apagar.
 *   - Suplementos com created_by_user_id = <uuid> são PRIVADOS do Personal Trainer.
 *
 * Funcionalidades:
 *   - Lista suplementos ativos/arquivados com pesquisa por nome
 *   - Badge "Global" para suplementos da plataforma
 *   - Criar, editar, arquivar, reativar e apagar os suplementos PRIVADOS
 */

import { useState, useEffect, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { matchesSearch } from '@/utils/validators';
import { toast } from 'react-toastify';
import {
  getSupplements,
  createSupplement,
  updateSupplement,
  archiveSupplement,
  unarchiveSupplement,
  deleteSupplement,
} from '@/api/supplementApi';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
  Table,
  TableHeader,
  TableRow,
  TableHead,
  TableBody,
  TableCell,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
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
  MoreHorizontal,
  Plus,
  Package,
  Archive,
  Trash2,
  Edit,
} from 'lucide-react';

export default function SupplementsPage() {
  // Estado principal
  const [supplements, setSupplements] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [tab, setTab] = useState('active'); // 'active' ou 'archived'

  // Dialog de criação/edição
  const [formOpen, setFormOpen] = useState(false);
  const [editingSupp, setEditingSupp] = useState(null); // null = criar novo, objeto = editar
  const [deleteTarget, setDeleteTarget] = useState(null); // suplemento a apagar (alert dialog)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      name: '',
      description: '',
      serving_size: '',
      timing: '',
      trainer_notes: '',
    },
  });

  // Um suplemento é global quando created_by_user_id é null ou undefined
  const isGlobal = (supplement) =>
    supplement.created_by_user_id === null ||
    supplement.created_by_user_id === undefined;

  // Carrega suplementos
  const loadSupplements = async () => {
    try {
      setLoading(true);
      // Carrega ativos e arquivados de uma vez
      const data = await getSupplements(true);
      setSupplements(data);
    } catch {
      toast.error('Erro ao carregar suplementos. Tente novamente.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSupplements();
  }, []);

  // Filtra suplementos para exibição com base na aba e pesquisa
  const filtered = useMemo(() => {
    return supplements.filter((supp) => {
      // Filtra por tab
      const isArchived = !!supp.archived_at;
      if (tab === 'active' && isArchived) return false;
      if (tab === 'archived' && !isArchived) return false;
      // Filtra por pesquisa
      if (
        search &&
        !matchesSearch(search, supp.name, supp.description, supp.timing)
      )
        return false;
      return true;
    });
  }, [supplements, tab, search]);

  // Abre dialog de criação (sem suplemento pré-carregado)
  const handleOpenCreate = () => {
    setEditingSupp(null);
    reset({
      name: '',
      description: '',
      serving_size: '',
      timing: '',
      trainer_notes: '',
    });
    setFormOpen(true);
  };

  // Abre dialog de edição com suplemento existente
  const handleOpenEdit = (supplement) => {
    if (isGlobal(supplement)) {
      toast.info('Suplementos globais não podem ser editados nesta página.');
      return;
    }
    setEditingSupp(supplement);
    // Pré-preenche o formulário se estivermos a editar
    reset({
      name: supplement?.name || '',
      description: supplement?.description || '',
      serving_size: supplement?.serving_size || '',
      timing: supplement?.timing || '',
      trainer_notes: supplement?.trainer_notes || '',
    });
    setFormOpen(true);
  };

  // Submete criação ou edição
  const onSubmit = async (data) => {
    // Remove campos  vazios para não enviar dados desnecessários à API
    const payload = Object.fromEntries(
      Object.entries(data).filter(([, value]) => value !== '')
    );

    try {
      if (editingSupp) {
        // Modo edição
        await updateSupplement(editingSupp.id, payload);
        toast.success('Suplemento atualizado com sucesso!');
      } else {
        // Modo criação
        await createSupplement(payload);
        toast.success('Suplemento criado com sucesso!');
      }
      setFormOpen(false);
      loadSupplements();
    } catch (error) {
      toast.error('Erro ao guardar suplemento.');
    }
  };

  // Arquiva ou reativa suplemento
  const handleArchive = async (supplement) => {
    try {
      await archiveSupplement(supplement.id);
      toast.success(`${supplement.name} arquivado.`);
      loadSupplements();
    } catch {
      toast.error('Erro ao arquivar suplemento.');
    }
  };

  const handleUnarchive = async (supplement) => {
    try {
      await unarchiveSupplement(supplement.id);
      toast.success(`${supplement.name} reativado.`);
      loadSupplements();
    } catch {
      toast.error('Erro ao reativar suplemento.');
    }
  };

  // Apaga suplemento (com confirmação)
  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteSupplement(deleteTarget.id);
      toast.success(`${deleteTarget.name} apagado.`);
      setDeleteTarget(null);
      loadSupplements();
    } catch {
      toast.error('Erro ao apagar suplemento.');
    }
  };

  return (
    <div className="p-6 space-y-6">
      {/* Cabeçalho */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Suplementos</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Catálogo de suplementos. Atribui suplementos a clientes na página de
            cada cliente.
          </p>
        </div>
        <Button onClick={handleOpenCreate}>
          <Plus className="mr-2 h-4 w-4" />
          Novo Suplemento
        </Button>
      </div>

      {/* Pesquisa + Tabs */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1 max-w-sm">
          <Input
            placeholder="Pesquisar por nome..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="active">Ativos</TabsTrigger>
            <TabsTrigger value="archived">Arquivados</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {/* Tabela */}
      {loading ? (
        <p className="text-muted-foreground text-sm">A carregar...</p>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-muted-foreground text-center">
          <Package className="h-10 w-10 mb-3 opacity-30" />
          <p className="font-medium">Nenhum suplemento encontrado.</p>
          <p className="text-sm">
            {tab === 'active'
              ? 'Cria o primeiro suplemento do teu catálogo.'
              : 'Sem suplementos arquivados.'}
          </p>
        </div>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead className="hidden md:table-cell">Dose</TableHead>
                <TableHead className="hidden md:table-cell">Timing</TableHead>
                <TableHead className="hidden lg:table-cell">
                  Descrição
                </TableHead>
                <TableHead className="w-10" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((supp) => {
                const global = isGlobal(supp);
                return (
                  <TableRow key={supp.id}>
                    <TableCell>
                      <div className="font-medium">{supp.name}</div>
                      {supp.archived_at && (
                        <Badge variant="secondary" className="mt-1 text-xs">
                          Arquivado
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="hidden md:table-cell text-muted-foreground">
                      {supp.serving_size || '—'}
                    </TableCell>
                    <TableCell className="hidden md:table-cell text-muted-foreground">
                      {supp.timing || '—'}
                    </TableCell>
                    <TableCell className="hidden lg:table-cell text-muted-foreground max-w-xs truncate">
                      {supp.description || '—'}
                    </TableCell>
                    <TableCell>
                      {/* Suplementos globais não têm menu de acções */}
                      {!global && (
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon">
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onClick={() => handleOpenEdit(supp)}
                            >
                              <Edit className="mr-2 h-4 w-4" /> Editar
                            </DropdownMenuItem>
                            {supp.archived_at ? (
                              <DropdownMenuItem
                                onClick={() => handleUnarchive(supp)}
                              >
                                <Package className="mr-2 h-4 w-4" /> Reativar
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem
                                onClick={() => handleArchive(supp)}
                              >
                                <Archive className="mr-2 h-4 w-4" /> Arquivar
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive"
                              onClick={() => setDeleteTarget(supp)}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Apagar
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Dialog: criar / editar */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {editingSupp ? 'Editar Suplemento' : 'Novo Suplemento'}
            </DialogTitle>
            <DialogDescription>
              {editingSupp
                ? 'Altera os dados do suplemento.'
                : 'Apenas o nome é obrigatório.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 pt-2">
            <div className="space-y-1">
              <Label htmlFor="s-name">
                Nome <span className="text-destructive">*</span>
              </Label>
              <Input
                id="s-name"
                placeholder="Ex: Creatina Monohidratada"
                {...register('name', { required: 'Nome obrigatório' })}
              />
              {errors.name && (
                <p className="text-destructive text-xs">
                  {errors.name.message}
                </p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="s-desc">Descrição</Label>
              <Textarea
                id="s-desc"
                rows={2}
                placeholder="Ex: Aumenta a força..."
                {...register('description')}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1">
                <Label htmlFor="s-dose">Dosagem</Label>
                <Input
                  id="s-dose"
                  placeholder="Ex: 5g"
                  {...register('serving_size')}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="s-timing">Timing</Label>
                <Input
                  id="s-timing"
                  placeholder="Ex: Pós-treino"
                  {...register('timing')}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="s-notes">
                Notas internas{' '}
                <span className="text-muted-foreground text-xs">
                  (não visíveis ao cliente)
                </span>
              </Label>
              <Textarea
                id="s-notes"
                rows={2}
                placeholder="Notas, marcas preferidas..."
                {...register('trainer_notes')}
              />
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <Button
                variant="outline"
                onClick={() => setFormOpen(false)}
                type="button"
              >
                Cancelar
              </Button>
              <Button onClick={handleSubmit(onSubmit)} disabled={isSubmitting}>
                {isSubmitting
                  ? 'A guardar...'
                  : editingSupp
                    ? 'Guardar'
                    : 'Criar Suplemento'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* AlertDialog: apagar */}
      <AlertDialog
        open={!!deleteTarget}
        onOpenChange={() => setDeleteTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Apagar suplemento?</AlertDialogTitle>
            <AlertDialogDescription>
              Vais apagar permanentemente <strong>{deleteTarget?.name}</strong>.
              Para manter histórico, arquiva em vez de apagar.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={handleDelete}
            >
              Apagar permanentemente
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
