import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'react-toastify';
import { useClients } from '../hooks/useClients';
import {
  createClient,
  archiveClient,
  updateClient,
  unarchiveClient,
} from '../api/clientsApi';
import { purchasePack } from '../api/packsApi';
import { generateInvite } from '../api/inviteApi';
import { createUser } from '../api/authApi';
import ClientTable from '@/components/clients/ClientTable';
import ClientFormDialog from '@/components/clients/ClientFormDialog';
import PackPurchaseDialog from '@/components/packs/PackPurchaseDialog';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Copy, MessageCircle, Check } from 'lucide-react';

/**
 * Página de gestão de clientes.
 *
 * Esta é a página "smart" (container) - contém a lógica de negócio:
 * - Busca dados via hook (useClients)
 * - Gere estados dos dialogs (form aberto/fechado, cliente selecionado)
 * - Executa operações CRUD via API
 * - Mostra feedback ao utilizador (toast)
 * - Delega a apresentação visual ao ClientTable e ClientFormDialog
 *
 * Padrão Container/Presentational:
 * Clients (smart) → ClientTable (visual) + ClientFormDialog  (visual) + PackPurchaseDialog (visual)
 */
export default function Clients() {
  const navigate = useNavigate();

  //Busca todos os clientes (sem filtro)
  const { clients, loading, error, refetch } = useClients({});

  //Estado do dialog de formulário (aberto/fechado)
  const [formOpen, setFormOpen] = useState(false);
  const [packDialogOpen, setPackDialogOpen] = useState(false); // Dialog de atribuir pack

  //Cliente atualmente selecionado para edição (null para criação)
  const [selectedClient, setSelectedClient] = useState(null);

  // Dialog de credenciais de convite
  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);
  const [inviteData, setInviteData] = useState(null); // { clientName, email, password, inviteLink, expiresInDays }
  const [copied, setCopied] = useState(false);

  // --- HANDLERS DE AÇÕES ---

  //Abrir dialog para criar novo cliente
  const handleAddClient = () => {
    setSelectedClient(null); //Limpa seleção
    setFormOpen(true); //Abre form
  };

  // Abrir dialog para editar cliente existente
  const handleEditClient = (client) => {
    setSelectedClient(client); //Seleciona cliente
    setFormOpen(true); //Abre form
  };

  //Navega para a página de detalhes do cliente
  const handleViewClient = (client) => {
    navigate(`/trainer/clientes/${client.id}`); //Redireciona para detalhes
  };

  //Submete o formulário de criação/edição
  const handleSave = async (data) => {
    try {
      if (selectedClient) {
        //Edição
        await updateClient(selectedClient.id, data);
        toast.success('Cliente atualizado com sucesso!');
      } else {
        //Criação: 1) cria o perfil do cliente, 2) cria a conta de utilizador
        const newClient = await createClient(data);

        // Cria a conta de utilizador associada ao cliente
        // Password temporária — será substituída quando o cliente usar o link de convite
        if (data.email) {
          try {
            const tempPassword = Math.floor(
              10000000 + Math.random() * 90000000
            ).toString();
            await createUser({
              email: data.email,
              password: tempPassword,
              full_name: data.full_name,
              role: 'client',
              client_id: newClient.id,
            });

            // Gera o convite e abre o dialog com as credenciais
            try {
              const inviteResponse = await generateInvite(newClient.id);
              setInviteData({
                clientName: data.full_name,
                email: data.email,
                password: tempPassword,
                inviteLink: inviteResponse.invite_link,
                expiresInDays: inviteResponse.expires_in_days,
              });
              setCopied(false);
              setInviteDialogOpen(true);
            } catch {
              // Se falhar a gerar o convite, mostra as credenciais sem link
              setInviteData({
                clientName: data.full_name,
                email: data.email,
                password: tempPassword,
                inviteLink: '(gera o link na tabela de clientes)',
                expiresInDays: 7,
              });
              setCopied(false);
              setInviteDialogOpen(true);
            }
          } catch {
            // Conta pode já existir — não bloqueia a criação do cliente
          }
        }

        toast.success('Cliente criado com sucesso!');
      }
      setFormOpen(false); //Fecha form
      refetch(); //Atualiza lista
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Ocorreu um erro ao salvar o cliente.'
      );
    }
  };

  //Arquivar ou reativa cliente
  const handleToggleArchive = async (client) => {
    try {
      if (client.status === 'active') {
        await archiveClient(client.id);
        toast.success(`${client.full_name} arquivado com sucesso!`);
      } else {
        await unarchiveClient(client.id);
        toast.success(`${client.full_name} reativado com sucesso!`);
      }
      refetch(); //Atualiza lista
    } catch (error) {
      toast.error(
        error.response?.data?.detail ||
          'Ocorreu um erro ao atualizar o status do cliente.'
      );
    }
  };

  // Gera link de convite e abre o dialog com as credenciais
  // Se o cliente não tiver conta, cria-a automaticamente e re-tenta
  const handleGenerateInvite = async (client) => {
    const openInviteDialog = (data, tempPassword = null) => {
      setInviteData({
        clientName: client.full_name,
        email: client.email,
        password: tempPassword,
        inviteLink: data.invite_link,
        expiresInDays: data.expires_in_days,
      });
      setCopied(false);
      setInviteDialogOpen(true);
    };

    try {
      const data = await generateInvite(client.id);
      openInviteDialog(data);
    } catch (error) {
      const detail = error.response?.data?.detail || '';

      // Cliente sem conta de utilizador — cria automaticamente e re-tenta
      if (error.response?.status === 404 && detail.includes('conta')) {
        if (!client.email) {
          toast.error(
            'Este cliente não tem email. Edita o cliente e adiciona um email primeiro.'
          );
          return;
        }
        try {
          const tempPassword = Math.floor(
            10000000 + Math.random() * 90000000
          ).toString();
          await createUser({
            email: client.email,
            password: tempPassword,
            full_name: client.full_name,
            role: 'client',
            client_id: client.id,
          });

          // Re-tenta gerar o convite
          const data = await generateInvite(client.id);
          openInviteDialog(data, tempPassword);
        } catch (retryError) {
          toast.error(
            retryError.response?.data?.detail ||
              'Erro ao criar conta e gerar convite.'
          );
        }
      } else {
        toast.error(detail || 'Erro ao gerar link de convite.');
      }
    }
  };

  // Monta a mensagem formatada com as credenciais
  const buildInviteMessage = () => {
    if (!inviteData) return '';
    let message =
      `Olá ${inviteData.clientName}! 🏋️\n\n` +
      `O teu Personal Trainer convidou-te para acederes à plataforma de treino.\n\n` +
      `Usa estas informações para fazer login:\n` +
      `📧 Login: ${inviteData.email}\n`;
    if (inviteData.password) {
      message += `🔒 Senha: ${inviteData.password}\n`;
    }
    message += `🔗 Link de acesso: ${inviteData.inviteLink}\n\n`;
    if (inviteData.password) {
      message += `Após o primeiro login, altera a tua password nas definições.`;
    } else {
      message += `Clica no link para definires a tua password e acederes ao teu plano.`;
    }
    return message;
  };

  // Copia a mensagem formatada com as credenciais
  const handleCopyCredentials = async () => {
    const message = buildInviteMessage();
    if (!message) return;
    await navigator.clipboard.writeText(message);
    setCopied(true);
    toast.success('Credenciais copiadas!');
    setTimeout(() => setCopied(false), 2000);
  };

  // Partilha as credenciais via WhatsApp
  const handleShareWhatsApp = () => {
    const message = buildInviteMessage();
    if (!message) return;
    window.open(
      `https://wa.me/?text=${encodeURIComponent(message)}`,
      '_blank',
      'noopener,noreferrer'
    );
  };

  // agendar sessão
  const handleScheduleSession = (client) => {
    // TODO: abrir dialog de agendar sessão com cliente pré-selecionado
    navigate('/sessoes');
  };

  //comprar pack
  const handlePurchasePack = (client) => {
    // TODO: abrir dialog de compra de pack com cliente pré-selecionado
    setSelectedClient(client); // Define cliente selecionado
    setPackDialogOpen(true); // Abre dialog de pack
  };

  // HANDLER DE COMPRA DE PACK
  /**
   * Processa a atribuição de um pack ao cliente.
   *
   * Fluxo:
   * 1. Recebe clientId e packTypeId do dialog
   * 2. Chama API para criar a compra
   * 3. Mostra feedback de sucesso/erro
   * 4. Atualiza lista de clientes (refetch)
   *
   * @param {string} clientId - ID do cliente
   * @param {string} packTypeId - ID do tipo de pack
   */

  const handlePackPurchase = async (clientId, packTypeId) => {
    try {
      // TODO: chamar API para criar compra de pack
      await purchasePack(clientId, { pack_type_id: packTypeId });
      toast.success('Pack atribuído com sucesso!');
      refetch(); // Atualiza lista de clientes para refletir novo pack
    } catch (error) {
      toast.error(
        error.response?.data?.detail ||
          'Ocorreu um erro ao atribuir o pack ao cliente.'
      );
      throw error; // Re-throw para permitir tratamento adicional no dialog, se necessário
    }
  };

  // --- RENDERIZAÇÃO ---
  if (error) {
    return (
      <div className="p-4 lg:p-6">
        <div className="text-center py-12 text-destructive">
          <p>Erro ao carregar clientes: {error}</p>
          <Button onClick={refetch} className="mt-4">
            Tentar novamente
          </Button>
        </div>
      </div>
    );
  }
  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Header da página */}
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Clientes</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Gerir a lista de clientes
        </p>
      </div>

      {/* LOADING STATE */}
      {loading ? (
        //loading skeleton
        <div className="space-y-3">
          {[1, 2, 3, 4, 5].map((i) => (
            <div
              key={i}
              className="h-14 rounded-lg bg-card border border-border animate-pulse"
            />
          ))}
        </div>
      ) : (
        // TABELA DE CLIENTES
        <ClientTable
          clients={clients}
          onAddClient={handleAddClient}
          onEditClient={handleEditClient}
          onViewClient={handleViewClient}
          onScheduleSession={handleScheduleSession}
          onPurchasePack={handlePurchasePack}
          onGenerateInvite={handleGenerateInvite}
          onArchiveClient={handleToggleArchive}
        />
      )}

      {/* Dialog de criação/edição */}
      <ClientFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        client={selectedClient}
        onSave={handleSave}
      />

      {/* DIALOG DE ATRIBUIÇÃO DE PACK */}
      <PackPurchaseDialog
        open={packDialogOpen}
        onOpenChange={setPackDialogOpen}
        client={selectedClient}
        onPurchase={handlePackPurchase}
      />

      {/* DIALOG DE CREDENCIAIS DE CONVITE */}
      <Dialog open={inviteDialogOpen} onOpenChange={setInviteDialogOpen}>
        <DialogContent className="sm:max-w-md bg-card border-border text-foreground">
          <DialogHeader>
            <DialogTitle>Convite Gerado</DialogTitle>
            <DialogDescription className="text-muted-foreground">
              Envia estas credenciais ao cliente para ele aceder à plataforma.
            </DialogDescription>
          </DialogHeader>

          {inviteData && (
            <div className="space-y-4 pt-2">
              {/* Card com as credenciais */}
              <div className="rounded-lg border border-border bg-muted p-4 space-y-2.5 font-mono text-sm">
                <p className="text-foreground font-sans font-medium text-base">
                  Dados de acesso para {inviteData.clientName}
                </p>
                <div className="border-t border-border pt-2.5 space-y-1.5">
                  <p className="text-muted-foreground">
                    <span className="font-sans font-medium text-foreground">
                      📧 Login:
                    </span>{' '}
                    {inviteData.email}
                  </p>
                  {inviteData.password && (
                    <p className="text-muted-foreground">
                      <span className="font-sans font-medium text-foreground">
                        🔒 Senha temporária:
                      </span>{' '}
                      {inviteData.password}
                    </p>
                  )}
                  <p className="text-muted-foreground break-all">
                    <span className="font-sans font-medium text-foreground">
                      🔗 Link:
                    </span>{' '}
                    {inviteData.inviteLink}
                  </p>
                </div>
                {inviteData.password ? (
                  <p className="text-xs text-muted-foreground font-sans pt-1">
                    O cliente deve alterar a password após o primeiro login.
                  </p>
                ) : (
                  <p className="text-xs text-muted-foreground font-sans pt-1">
                    O cliente define a password ao clicar no link.
                  </p>
                )}
              </div>

              {/* Acções */}
              <div className="flex flex-wrap gap-2">
                <Button
                  onClick={handleCopyCredentials}
                  className="flex-1"
                  variant={copied ? 'outline' : 'default'}
                >
                  {copied ? (
                    <>
                      <Check className="h-4 w-4 mr-2" /> Copiado!
                    </>
                  ) : (
                    <>
                      <Copy className="h-4 w-4 mr-2" /> Copiar Mensagem
                    </>
                  )}
                </Button>
                <Button
                  onClick={handleShareWhatsApp}
                  className="flex-1 bg-[#25D366] hover:bg-[#1ebe5d] text-white border-0"
                >
                  <MessageCircle className="h-4 w-4 mr-2" />
                  Enviar via WhatsApp
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
