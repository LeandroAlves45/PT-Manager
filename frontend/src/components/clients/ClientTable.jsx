import { useState, useMemo } from 'react';
import { matchesSearch } from '@/utils/validators';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
  DropdownMenuItem,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  MoreHorizontal,
  Search,
  Plus,
  Edit,
  Eye,
  Archive,
  ArchiveRestore,
  CalendarPlus,
  Package,
  Link,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { getInitials, getStatusColor, getStatusLabel } from '@/lib/helpers';
import { Button } from '@/components/ui/button';

/**
 * Tabela de clientes
 * -Pesquisa por nome, telefone, email
 * -Filtro de tabs: Ativos, Arquivados, Todos
 * -Dropdown de ações: Ver, Editar, Arquivar/Reativar, Agendar sessão, Gerar plano
 *
 * Este componente é "burro" - recebe dados e callbacks via props, sem lógica de negócios.
 * A lógica de busca, filtragem, ações é gerenciada pelo componente pai (ex: ClientListPage).
 */
export default function ClientTable({
  clients,
  onAddClient,
  onEditClient,
  onViewClient,
  onArchiveClient,
  onScheduleSession,
  onPurchasePack,
  onGenerateInvite,
}) {
  //Estado Local de componente
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');

  /**
   * useMemo para memoizar a lista filtrada de clientes, evitando cálculos desnecessários em cada renderização.
   */
  const filtered = useMemo(() => {
    let result = clients;

    //filtro de status
    if (statusFilter === 'active') {
      result = result.filter((c) => c.status === 'active');
    } else if (statusFilter === 'archived') {
      result = result.filter((c) => c.status === 'archived');
    }

    if (search.trim()) {
      const s = search.toLowerCase();
      result = result.filter((c) => matchesSearch(s, c.full_name, c.phone, c.email));
    }

    return result;
  }, [clients, search, statusFilter]);

  return (
    <div className="flex flex-col gap-4">
      {/* Barra superior: pesquisa + Botão Novo Cliente */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        {/* CAMPO DE PESQUISA */}
        <div className="relative w-full sm:w-80">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Pesquisar por nome, telemóvel ou email..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9 bg-background border-input text-foreground"
          />
        </div>
        <Button
          onClick={onAddClient}
          className="bg-primary text-primary-foreground hover:bg-primary/90"
        >
          <Plus className="h-4 w-4 mr-1.5" />
          Novo Cliente
        </Button>
      </div>

      {/* TABS DE FILTRO */}
      <Tabs value={statusFilter} onValueChange={setStatusFilter}>
        <TabsList className="bg-secondary">
          <TabsTrigger
            value="all"
            className="data-[state=active]:bg-background data-[state=active]:text-foreground"
          >
            Todos ({clients.length})
          </TabsTrigger>
          <TabsTrigger
            value="active"
            className="data-[state=active]:bg-background data-[state=active]:text-foreground"
          >
            Ativos ({clients.filter((c) => c.status === 'active').length})
          </TabsTrigger>
          <TabsTrigger
            value="archived"
            className="data-[state=active]:bg-background data-[state=active]:text-foreground"
          >
            Arquivados ({clients.filter((c) => c.status === 'archived').length})
          </TabsTrigger>
        </TabsList>
      </Tabs>

      {/* TABELA DE CLIENTES */}
      <div className="rounded-xl border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-muted-foreground">Cliente</TableHead>
              <TableHead className="text-muted-foreground hidden md:table-cell">
                Telemóvel
              </TableHead>
              <TableHead className="text-muted-foreground hidden lg:table-cell">
                Email
              </TableHead>
              <TableHead className="text-muted-foreground hidden sm:table-cell">
                Pack Ativo
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
                  colSpan={6}
                  className="text-center py-8 text-muted-foreground"
                >
                  Nenhum cliente encontrado.
                </TableCell>
              </TableRow>
            ) : (
              filtered.map((client) => (
                <TableRow
                  key={client.id}
                  className="border-border hover:bg-accent/50 cursor-pointer"
                  onClick={() => onViewClient(client)}
                >
                  {/*Coluna: avatar + nome */}
                  <TableCell>
                    <div className="flex items-center gap-3">
                      <Avatar className="h-8 w-8">
                        <AvatarFallback className="bg-primary/10 text-primary text-xs font-semibold">
                          {getInitials(client.full_name)}
                        </AvatarFallback>
                      </Avatar>
                      <span className="text-sm font-medium text-foreground">
                        {client.full_name}
                      </span>
                    </div>
                  </TableCell>

                  {/*Coluna: telemóvel */}
                  <TableCell className="text-sm text-muted-foreground hidden md:table-cell">
                    {client.phone}
                  </TableCell>

                  {/*Coluna: email */}
                  <TableCell className="text-sm text-muted-foreground hidden lg:table-cell">
                    {client.email}
                  </TableCell>

                  {/*Coluna: pack ativo com sessões usadas/total */}
                  <TableCell className="hidden sm:table-cell">
                    {client.active_pack ? (
                      <Badge
                        variant="outline"
                        className="bg-primary/10 text-primary border-primary/20 text-xs"
                      >
                        {client.active_pack.pack_type_name}{' '}
                        <span className="ml-1 text-muted-foreground">
                          ({client.active_pack.sessions_used}/
                          {client.active_pack.sessions_total})
                        </span>
                      </Badge>
                    ) : (
                      <span className="text-sm text-muted-foreground">
                        Sem pack
                      </span>
                    )}
                  </TableCell>

                  {/*Coluna: status */}
                  <TableCell>
                    <Badge
                      variant="outline"
                      className={cn('text-xs', getStatusColor(client.status))}
                    >
                      {getStatusLabel(client.status)}
                    </Badge>
                  </TableCell>

                  {/*Coluna: ações */}
                  <TableCell className="text-right">
                    <DropdownMenu>
                      {/* stopPropagation evita que o click no botão dispare o onClick da row */}
                      <DropdownMenuTrigger
                        asChild
                        onClick={(e) => e.stopPropagation()}
                      >
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
                          onClick={(e) => {
                            e.stopPropagation();
                            onViewClient(client);
                          }}
                        >
                          <Eye className="h-4 w-4 mr-2" /> Ver Detalhes
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={(e) => {
                            e.stopPropagation();
                            onEditClient(client);
                          }}
                        >
                          <Edit className="h-4 w-4 mr-2" /> Editar Cliente
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={(e) => {
                            e.stopPropagation();
                            onScheduleSession(client);
                          }}
                        >
                          <CalendarPlus className="h-4 w-4 mr-2" /> Agendar
                          Sessão
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={(e) => {
                            e.stopPropagation();
                            onPurchasePack(client);
                          }}
                        >
                          <Package className="h-4 w-4 mr-2" /> Comprar Pack
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={(e) => {
                            e.stopPropagation();
                            onGenerateInvite(client);
                          }}
                        >
                          <Link className="h-4 w-4 mr-2" /> Gerar Link de Convite
                        </DropdownMenuItem>
                        <DropdownMenuSeparator className="bg-border" />
                        <DropdownMenuItem
                          onClick={(e) => {
                            e.stopPropagation();
                            onArchiveClient(client);
                          }}
                          className={
                            client.status === 'active'
                              ? 'text-destructive'
                              : 'text-success'
                          }
                        >
                          {client.status === 'active' ? (
                            <>
                              <Archive className="h-4 w-4 mr-2" /> Arquivar
                              Cliente
                            </>
                          ) : (
                            <>
                              <ArchiveRestore className="h-4 w-4 mr-2" /> Ativar
                              Cliente
                            </>
                          )}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
