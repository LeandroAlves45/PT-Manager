import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import {
  ClipboardList,
  Copy,
  Users,
  MoreHorizontal,
  Edit,
  Trash2,
  Eye,
} from 'lucide-react';

/**
 * Retorna classes de cor e label para cada status de plano.
 * Centralizado aqui para não duplicar lógica entre PlanList e outros componentes.
 *
 * @param {string} status - 'draft' | 'published' | 'archived'
 */

function getStatusStyle(status) {
  switch (status) {
    case 'published':
      return {
        className: 'border-green-500/50 text-green-500',
        label: 'Publicado',
      };
    case 'draft':
      return {
        className: 'border-yellow-500/50 text-yellow-500',
        label: 'Rascunho',
      };
    case 'archived':
      return {
        className: 'border-muted text-muted-foreground',
        label: 'Arquivado',
      };
    default:
      return { className: '', label: 'status' };
  }
}

/**
 * Tabela que lista os planos de treino filtrados.
 * A lógica de filtro (search, tabs) fica na página pai (TrainingPlans.jsx)
 * para separar responsabilidades: este componente só renderiza, não filtra.
 *
 * @param {Array} plans - Lista de planos já filtrados
 * @param {Array} clients - Lista de clientes (para cruzar o nome pelo client_id)
 * @param {Function} onView - Navega para detalhe do plano
 * @param {Function} onEdit - Abre dialog de edição
 * @param {Function} onDelete - Remove o plano (com confirmação no pai)
 */

export default function PlanList({
  plans = [],
  clients = [],
  onView,
  onEdit,
  onDelete,
}) {
  if (plans.length === 0) {
    return (
      <div className="rounded-xl border border-border">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-muted-foreground">Plano</TableHead>
              <TableHead className="text-muted-foreground hidden md:table-cell">
                Tipo
              </TableHead>
              <TableHead className="text-muted-foreground hidden lg:table-cell">
                Cliente
              </TableHead>
              <TableHead className="text-muted-foreground">Status</TableHead>
              <TableHead className="text-muted-foreground text-right">
                Ações
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell
                colSpan={5}
                className="text-center py-12 text-muted-foreground"
              >
                Nenhum plano encontrado.
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-border overflow-hidden">
      <Table>
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <TableHead className="text-muted-foreground">Plano</TableHead>
            <TableHead className="text-muted-foreground hidden md:table-cell">
              Tipo
            </TableHead>
            <TableHead className="text-muted-foreground hidden lg:table-cell">
              Cliente
            </TableHead>
            <TableHead className="text-muted-foreground">Status</TableHead>
            <TableHead className="text-muted-foreground text-right">
              Ações
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {plans.map((plan) => {
            // Encontra o cliente associado ao plano, se houver
            const client = clients.find((c) => c.id === plan.client_id);
            const isTemplate = plan.client_id === null;
            const statusStyle = getStatusStyle(plan.status);

            return (
              <TableRow
                key={plan.id}
                className="border-border hover:bg-accent/50 cursor-pointer"
                //Clique na linha abre o detalhe do plano
                onClick={() => onView(plan)}
              >
                {/* Nome do plano */}
                <TableCell>
                  <div className="flex items-center gap-2">
                    <ClipboardList className="h-4 w-4 text-primary shrink-0" />
                    <span className="text-sm font-medium text-foreground">
                      {plan.name}
                    </span>
                  </div>
                </TableCell>

                {/* Tipo: Template ou Personalizado */}
                <TableCell className="hidden md:table-cell">
                  {isTemplate ? (
                    <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                      <Copy className="h-3.5 w-3.5" />
                      <span>Template</span>
                    </div>
                  ) : (
                    <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                      <Users className="h-3.5 w-3.5" />
                      <span>Personalizado</span>
                    </div>
                  )}
                </TableCell>

                {/* Nome do cliente, ou "Sem cliente" se for template */}
                <TableCell className="hidden lg:table-cell">
                  <span className="text-sm text-foreground">
                    {client?.full_name ?? '-'}
                  </span>
                </TableCell>

                {/* Status com badge */}
                <TableCell>
                  <Badge
                    variant="outline"
                    className={cn(`text-xs ${statusStyle.className}`)}
                  >
                    {statusStyle.label}
                  </Badge>
                </TableCell>

                {/* Ações: Editar, Excluir, Ver - stopPropagation para não disparar o onClick da linha*/}
                <TableCell
                  className="text-right"
                  onClick={(e) => e.stopPropagation()}
                >
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
                        onClick={() => onView(plan)}
                        className="gap-2 cursor-pointer"
                      >
                        <Eye className="h-4 w-4" /> Ver Plano
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        onClick={() => onEdit(plan)}
                        className="gap-2 cursor-pointer"
                      >
                        <Edit className="h-4 w-4" /> Editar Plano
                      </DropdownMenuItem>
                      <DropdownMenuSeparator className="bg-border" />
                      <DropdownMenuItem
                        onClick={() => onDelete(plan)}
                        className="gap-2 cursor-pointer text-destructive focus:text-destructive"
                      >
                        <Trash2 className="h-4 w-4" /> Excluir Plano
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
