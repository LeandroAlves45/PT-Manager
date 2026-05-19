import { useState, useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import {
  getPackTypes,
  createPackType,
  updatePackType,
  deletePackType,
} from '@/api/packTypesApi';
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
} from '@/components/ui/dropdown-menu';
import { MoreHorizontal, Plus, Edit, Trash2 } from 'lucide-react';
import { cn } from '@/lib/utils';

export default function Packs() {
  const [packTypes, setPackTypes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingPack, setEditingPack] = useState(null);
  const [deleteConfirm, setDeleteConfirm] = useState(null);

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors },
  } = useForm({
    defaultValues: { name: '', sessions_total: 8, is_active: true },
  });

  //Bsuca pack types da API
  const fetchPackTypes = async () => {
    setLoading(true);
    try {
      const data = await getPackTypes();
      setPackTypes(data);
    } catch (err) {
      toast.error('Erro ao carregar pack types');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPackTypes();
  }, []);

  //Abre dialog para criar novo pack
  const openAdd = () => {
    setEditingPack(null);
    reset({ name: '', sessions_total: 8, is_active: true });
    setDialogOpen(true);
  };
  //Abre dialog para editar pack existente
  const openEdit = (pack) => {
    setEditingPack(pack);
    reset({
      name: pack.name,
      sessions_total: pack.sessions_total,
      is_active: pack.is_active,
    });
    setDialogOpen(true);
  };

  const onSubmit = async (data) => {
    try {
      if (editingPack) {
        await updatePackType(editingPack.id, data);
        toast.success('Pack atualizado com sucesso!');
      } else {
        await createPackType(data);
        toast.success('Pack criado com sucesso!');
      }
      setDialogOpen(false);
      fetchPackTypes();
    } catch (err) {
      toast.error(err.response?.data?.detail || 'Erro ao salvar pack');
    }
  };

  const handleDelete = async () => {
    if (!deleteConfirm) return;
    try {
      await deletePackType(deleteConfirm.id);
      toast.success(`Pack "${deleteConfirm.name}" eliminado`);
      setDeleteConfirm(null);
      fetchPackTypes();
    } catch (err) {
      toast.error(err.response?.data?.detail || 'Erro ao eliminar pack');
    }
  };

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">
            Tipos de Pack
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Gerir os tipos de packs disponíveis
          </p>
        </div>
        <Button
          onClick={openAdd}
          className="bg-primary text-primary-foreground hover:bg-primary/90"
        >
          <Plus className="h-4 w-4 mr-1.5" /> Novo Tipo de Pack
        </Button>
      </div>

      {loading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="h-14 rounded-lg bg-card border border-border animate-pulse"
            />
          ))}
        </div>
      ) : (
        <div className="rounded-xl border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="border-border hover:bg-transparent">
                <TableHead className="text-muted-foreground">Nome</TableHead>
                <TableHead className="text-muted-foreground">
                  Total Sessões
                </TableHead>
                <TableHead className="text-muted-foreground">Estado</TableHead>
                <TableHead className="text-muted-foreground text-right">
                  Ações
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {packTypes.map((pack) => (
                <TableRow
                  key={pack.id}
                  className="border-border hover:bg-accent/50"
                >
                  <TableCell className="text-sm font-medium text-foreground">
                    {pack.name}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {pack.sessions_total} sessões
                  </TableCell>
                  <TableCell>
                    <Badge
                      variant="outline"
                      className={cn(
                        'text-xs',
                        pack.is_active
                          ? 'bg-success/15 text-success border-success/20'
                          : 'bg-muted text-muted-foreground border-border'
                      )}
                    >
                      {pack.is_active ? 'Ativo' : 'Inativo'}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right">
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
                        <DropdownMenuItem onClick={() => openEdit(pack)}>
                          <Edit className="h-4 w-4 mr-2" /> Editar
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={() => setDeleteConfirm(pack)}
                          className="text-destructive"
                        >
                          <Trash2 className="h-4 w-4 mr-2" /> Eliminar
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Dialog criação/edição */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {editingPack ? 'Editar Tipo de Pack' : 'Novo Tipo de Pack'}
            </DialogTitle>
            <DialogDescription className="text-muted-foreground">
              {editingPack
                ? 'Atualize os dados do tipo de pack.'
                : 'Preencha os dados para criar um novo tipo de pack.'}
            </DialogDescription>
          </DialogHeader>
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="flex flex-col gap-4 mt-2"
          >
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="name">Nome *</Label>
              <Input
                id="name"
                {...register('name', { required: 'Nome é obrigatório' })}
                className="bg-background border-input text-foreground"
                placeholder="Ex: Pack 8 Sessões"
              />
              {errors.name && (
                <span className="text-xs text-destructive">
                  {errors.name.message}
                </span>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="sessions_total">Total de Sessões *</Label>
              <Input
                id="sessions_total"
                type="number"
                {...register('sessions_total', {
                  required: 'Obrigatório',
                  valueAsNumber: true,
                  min: { value: 1, message: 'Mínimo 1' },
                })}
                className="bg-background border-input text-foreground"
              />
              {errors.sessions_total && (
                <span className="text-xs text-destructive">
                  {errors.sessions_total.message}
                </span>
              )}
            </div>
            <div className="flex items-center justify-between">
              <Label>Ativo</Label>
              <Switch
                checked={watch('is_active')}
                onCheckedChange={(checked) => setValue('is_active', checked)}
              />
            </div>
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
                {editingPack ? 'Guardar Alterações' : 'Criar Pack'}
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      {/* Dialog confirmação de eliminação */}
      <AlertDialog
        open={!!deleteConfirm}
        onOpenChange={(open) => !open && setDeleteConfirm(null)}
      >
        <AlertDialogContent className="bg-card border-border text-foreground">
          <AlertDialogHeader>
            <AlertDialogTitle className="text-lg font-semibold">
              Eliminar Tipo de Pack
            </AlertDialogTitle>
            <AlertDialogDescription className="text-muted-foreground">
              Tem a certeza que deseja eliminar o pack? Esta ação não pode ser
              desfeita.
            </AlertDialogDescription>
          </AlertDialogHeader>

          <AlertDialogFooter className="flex justify-end gap-3 pt-4">
            <AlertDialogCancel className="border-border text-foreground">
              Cancelar
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
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
