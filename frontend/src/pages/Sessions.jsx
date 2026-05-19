import { useState, useMemo } from 'react';
import { matchesSearch } from '@/utils/validators';
import { toast } from 'react-toastify';
import { useSessions } from '@/hooks/useSessions';
import {
  createSession,
  completeSession,
  markSessionMissed,
  cancelSession,
} from '@/api/sessionApi';
import SessionFormDialog from '@/components/sessions/SessionFormDialog';
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
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
  MoreHorizontal,
  Search,
  CalendarPlus,
  CheckCircle,
  XCircle,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  getInitials,
  formatDateTime,
  getStatusColor,
  getStatusLabel,
} from '@/lib/helpers';

/**
 * Página de gestão de sessões de treino.
 *
 * Mostra todas as sessões com filtros por status e pesquisa por nome de cliente.
 * Ações disponíveis: Concluir, Marcar Falta, Cancelar.
 */

export default function Sessions() {
  const { sessions, loading, error, refetch } = useSessions({ limit: 200 });
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');

  // Estado do dialog de criação
  const [formOpen, setFormOpen] = useState(false);

  //Filtra sessões por status e pesquisa
  const filtered = useMemo(() => {
    let result = sessions;
    if (statusFilter !== 'all')
      result = result.filter((s) => s.status === statusFilter);
    if (search.trim()) {
      const q = search.toLowerCase();
      result = result.filter((s) => matchesSearch(q, s.client?.name));
    }
    return result;
  }, [sessions, search, statusFilter]);

  //Ações de sessão

  /**
   * Abre o dialog para agendar nova sessão.
   */
  const handleScheduleSession = () => {
    setFormOpen(true);
  };

  /**
   * Cria uma nova sessão de treino.
   *
   * Recebe dados do formulário já formatados:
   * - client_id: UUID do cliente
   * - starts_at: Data/hora em formato ISO
   * - duration_minutes: Número (minutos)
   * - location: String ou null
   * - notes: String ou null
   *
   * @param {Object} sessionData - Dados da sessão a criar
   */

  const handleSave = async (sessionData) => {
    try {
      //Extrai client_id do payload
      const { client_id, ...data } = sessionData;
      await createSession(client_id, data);
      toast.success('Sessão agendada com sucesso');
      setFormOpen(false);
      try {
        await refetch(); //Atualiza lista após criação
      } catch (e) {
        toast.error(
          e.response?.data?.message || 'Erro ao atualizar lista de sessões'
        );
        window.location.reload(); //Fallback: recarrega página se refetch falhar
      }
    } catch (e) {
      toast.error(e.response?.data?.message || 'Erro ao agendar sessão');
    }
  };

  /**
   * Marca uma sessão como concluída.
   *
   * Efeitos:
   * - Status muda para "completed"
   * - Consome 1 sessão do pack do cliente
   * - Atualiza contadores de sessões usadas
   *
   * @param {Object} session - Sessão a concluir
   */

  const handleComplete = async (session) => {
    try {
      await completeSession(session.id);
      toast.success(`Sessão de ${session.client_name} concluída`);
      refetch();
    } catch (e) {
      toast.error(e.response?.data?.message || 'Erro ao concluir sessão');
    }
  };

  /**
   * Marca uma sessão como falta.
   *
   * Efeitos:
   * - Status muda para "missed"
   * - Consome 1 sessão do pack do cliente
   * - Regista falta no histórico
   *
   * @param {Object} session - Sessão a marcar como falta
   */

  const handleMissed = async (session) => {
    try {
      await markSessionMissed(session.id);
      toast.success(`Falta registada para ${session.client_name}`);
      try {
        await refetch(); //Atualiza lista após criação
      } catch (e) {
        toast.error(
          e.response?.data?.message || 'Erro ao atualizar lista de sessões'
        );
        window.location.reload(); //Fallback: recarrega página se refetch falhar
      }
    } catch (e) {
      toast.error(e.response?.data?.message || 'Erro ao marcar falta');
    }
  };

  /**
   * Cancela uma sessão agendada.
   *
   * Efeitos:
   * - Status muda para "cancelled"
   * - NÃO consome sessão do pack
   * - Pode ser reagendada posteriormente
   *
   * @param {Object} session - Sessão a cancelar
   */

  const handleCancel = async (session) => {
    try {
      await cancelSession(session.id);
      toast.success(`Sessão de ${session.client_name} cancelada`);
      try {
        await refetch(); //Atualiza lista após cancelamento
      } catch (e) {
        toast.error(
          e.response?.data?.message || 'Erro ao atualizar lista de sessões'
        );
        window.location.reload(); //Fallback: recarrega página se refetch falhar
      }
    } catch (e) {
      toast.error(e.response?.data?.message || 'Erro ao cancelar sessão');
    }
  };

  // ============================================================================
  // RENDERIZAÇÃO
  // ============================================================================

  if (error) {
    return (
      <div className="p-4 lg:p-6">
        <div className="text-center py-12 text-destructive">
          <p>Erro ao carregar sessões: {error}</p>
          <Button onClick={refetch} className="mt-4">
            Tentar novamente
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* HEADER DA PÁGINA */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Sessões</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Gerir sessões de treino
          </p>
        </div>

        {/* BOTÃO: Nova Sessão */}
        <Button
          onClick={handleScheduleSession}
          className="bg-primary text-primary-foreground hover:bg-primary/90 gap-2"
        >
          <CalendarPlus className="h-4 w-4" />
          Nova Sessão
        </Button>
      </div>

      {/* FILTROS E PESQUISA */}
      <div className="flex flex-col sm:flex-row gap-3">
        {/* Campo de pesquisa */}
        <div className="relative w-full sm:w-80">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground h-4 w-4" />
          <Input
            placeholder="Pesquisar por cliente..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9 bg-background border-input text-foreground"
          />
        </div>

        {/* Tabs de filtro por status */}
        <Tabs value={statusFilter} onValueChange={setStatusFilter}>
          <TabsList className="bg-secondary">
            <TabsTrigger
              value="all"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Todas
            </TabsTrigger>
            <TabsTrigger
              value="scheduled"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Agendadas
            </TabsTrigger>
            <TabsTrigger
              value="completed"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Concluídas
            </TabsTrigger>
            <TabsTrigger
              value="missed"
              className="data-[state=active]:bg-background data-[state=active]:text-foreground"
            >
              Faltas
            </TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {/* TABELA DE SESSÕES */}
      {loading ? (
        // Loading skeleton - mostra placeholders enquanto carrega
        <div className="space-y-3">
          {[1, 2, 3, 4, 5].map((i) => (
            <div
              key={i}
              className="h-14 rounded-lg bg-card border border-border animate-pulse"
            />
          ))}
        </div>
      ) : (
        // Tabela com dados reais
        <div className="rounded-xl border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="border-border hover:bg-transparent">
                <TableHead className="text-muted-foreground">Cliente</TableHead>
                <TableHead className="text-muted-foreground hidden sm:table-cell">
                  Data / Hora
                </TableHead>
                <TableHead className="text-muted-foreground hidden md:table-cell">
                  Duração
                </TableHead>
                <TableHead className="text-muted-foreground hidden lg:table-cell">
                  Local
                </TableHead>
                <TableHead className="text-muted-foreground">Estado</TableHead>
                <TableHead className="text-muted-foreground text-right">
                  Ações
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 ? (
                // Estado vazio - nenhuma sessão encontrada
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="text-center py-8 text-muted-foreground"
                  >
                    Nenhuma sessão encontrada.
                  </TableCell>
                </TableRow>
              ) : (
                // Lista de sessões
                filtered.map((session) => (
                  <TableRow
                    key={session.id}
                    className="border-border hover:bg-accent/50"
                  >
                    {/* COLUNA: Cliente */}
                    <TableCell>
                      <div className="flex items-center gap-3">
                        <Avatar className="h-8 w-8">
                          <AvatarFallback className="bg-primary/10 text-primary text-xs font-semibold">
                            {getInitials(session.client_name || '??')}
                          </AvatarFallback>
                        </Avatar>
                        <span className="text-sm font-medium text-foreground">
                          {session.client_name || 'Cliente'}
                        </span>
                      </div>
                    </TableCell>

                    {/* COLUNA: Data/Hora (hidden em mobile) */}
                    <TableCell className="text-sm text-muted-foreground hidden sm:table-cell">
                      {formatDateTime(session.starts_at)}
                    </TableCell>

                    {/* COLUNA: Duração (hidden em mobile e tablet) */}
                    <TableCell className="text-sm text-muted-foreground hidden md:table-cell">
                      {session.duration_minutes} min
                    </TableCell>

                    {/* COLUNA: Local (hidden em mobile, tablet e desktop pequeno) */}
                    <TableCell className="text-sm text-muted-foreground hidden lg:table-cell">
                      {session.location || 'Não especificado'}
                    </TableCell>

                    {/* COLUNA: Status */}
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={cn(
                          'text-xs',
                          getStatusColor(session.status)
                        )}
                      >
                        {getStatusLabel(session.status)}
                      </Badge>
                    </TableCell>

                    {/* COLUNA: Ações */}
                    <TableCell className="text-right">
                      {/* Ações disponíveis apenas para sessões agendadas */}
                      {session.status === 'scheduled' && (
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
                            {/* Ação: Concluir */}
                            <DropdownMenuItem
                              onClick={() => handleComplete(session)}
                            >
                              <CheckCircle className="h-4 w-4 mr-2 text-success" />
                              Concluir
                            </DropdownMenuItem>

                            {/* Ação: Marcar Falta */}
                            <DropdownMenuItem
                              onClick={() => handleMissed(session)}
                            >
                              <XCircle className="h-4 w-4 mr-2" />
                              Marcar Falta
                            </DropdownMenuItem>

                            <DropdownMenuSeparator className="bg-border" />

                            {/* Ação: Cancelar */}
                            <DropdownMenuItem
                              onClick={() => handleCancel(session)}
                              className="text-destructive"
                            >
                              <XCircle className="h-4 w-4 mr-2" />
                              Cancelar
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {/* DIALOG DE CRIAÇÃO DE SESSÃO */}
      <SessionFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        onSave={handleSave}
      />
    </div>
  );
}
