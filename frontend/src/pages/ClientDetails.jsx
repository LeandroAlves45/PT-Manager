import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { toast } from 'react-toastify';
import { getClient } from '../api/clientsApi';
import { getSessions } from '../api/sessionApi';
import { generateInvite } from '../api/inviteApi';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Progress } from '@/components/ui/progress';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  ArrowLeft,
  Mail,
  Phone,
  Calendar,
  Ruler,
  Link,
  Copy,
  MessageCircle,
  User,
  Dumbbell,
  KeyRound,
} from 'lucide-react';
import {
  getInitials,
  calculateAge,
  formatDate,
  getSexLabel,
  getStatusColor,
  getStatusLabel,
} from '@/lib/helpers';

/**
 * ClientDetails — Página de detalhes de um cliente (rota /trainer/clientes/:id)
 *
 * Estrutura em 4 tabs:
 *   Perfil        — informação pessoal e pack ativo
 *   Acesso        — email + geração/cópia de link de convite + partilha WhatsApp
 *   Suplementação — atribuição e lista de suplementos
 *   Sessões       — histórico das últimas 10 sessões
 *
 * Razão de negócio: agregar toda a informação do cliente num único ecrã evita
 * que o Personal Trainer tenha de navegar para outros contextos
 */

export default function ClientDetails() {
  //useParams extrai parâmetros dinâmicos da URL
  const { id } = useParams();
  const navigate = useNavigate();

  const [client, setClient] = useState(null);
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);

  // Tab ativa, começa no Perfil
  const [activeTab, setActiveTab] = useState('perfil');

  // Link de convite
  const [inviteLink, setInviteLink] = useState('');
  const [generatingInvite, setGeneratingInvite] = useState(false);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      try {
        //Busca detalhes do cliente
        const clientData = await getClient(id);
        setClient(clientData);
        //Busca sessões do cliente
        const sessionsData = await getSessions({ client_id: id });
        setSessions(sessionsData);
      } catch (error) {
        toast.error('Erro ao carregar dados do cliente');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [id]);

  // Gera um link de convite para o cliente
  const handleGenerateInvite = async () => {
    setGeneratingInvite(true);
    try {
      const data = await generateInvite(id);
      setInviteLink(data.invite_link);
      // Copia o link para a área de transferência
      await navigator.clipboard.writeText(data.invite_link);
      toast.success(
        `Link de convite copiado para a área de transferência! Válido por ${data.expires_in_days} dias.`
      );
    } catch {
      toast.error('Erro ao gerar link de convite.');
    } finally {
      setGeneratingInvite(false);
    }
  };

  // Copia o link de convite para a área de transferência
  const handleCopyInvite = async () => {
    if (!inviteLink) return;
    await navigator.clipboard.writeText(inviteLink);
    toast.success('Link de convite copiado para a área de transferência!');
  };

  // Abre com Whatsapp com mensagem pré formatada
  const handleShareWhatsApp = () => {
    if (!inviteLink) return;
    const clientName = client ? client.full_name : 'cliente';
    const message =
      `Olá ${clientName}! O teu Personal Trainer convidou-te para acederes à plataforma de treino.\n\n` +
      `Clica no link abaixo para definires a tua password e acederes ao teu plano:\n` +
      `${inviteLink}\n\n` +
      `O link é válido por 7 dias e só pode ser usado uma vez.`;
    window.open(
      `https://wa.me/?text=${encodeURIComponent(message)}`,
      '_blank',
      'noopener,noreferrer'
    );
  };

  // Derivados
  // Progresso do pack ativo, para a barra de progresso
  const packProgress = client?.active_pack
    ? (client.active_pack.sessions_used / client.active_pack.sessions_total) *
      100
    : 0;

  if (loading) {
    return (
      <div className="p-4 lg:p-6">
        <div className="space-y-4">
          <div className="h-8 w-48 rounded bg-card animate-pulse" />
          <div className="h-48 rounded-xl bg-card animate-pulse" />
        </div>
      </div>
    );
  }

  if (!client) {
    return (
      <div className="p-4 lg:p-6 text-center py-12">
        <p className="text-muted-foreground">Cliente não encontrado</p>
        <Button onClick={() => navigate('/clients')} className="mt-4">
          Voltar
        </Button>
      </div>
    );
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Botão de regresso à lista de clientes */}
      <Button
        variant="ghost"
        onClick={() => navigate('/trainer/clientes')}
        className="w-fit text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4 mr-2" /> Voltar aos Clientes
      </Button>

      {/* Cabeçalho - identidade do cliente (sempre visível, fora das tabs) */}
      <Card className="bg-card border-border">
        <CardContent className="p-6">
          <div className="flex flex-col sm:flex-row items-start gap-4">
            {/* Avatar com iniciais */}
            <Avatar className="h-16 w-16">
              <AvatarFallback className="bg-primary/10 text-primary text-xl font-semibold">
                {getInitials(client.full_name)}
              </AvatarFallback>
            </Avatar>

            <div className="flex-1">
              <div className="flex items-center gap-3 mb-2">
                <h1 className="text-xl font-semibold text-foreground">
                  {client.full_name}
                </h1>
                <Badge
                  variant="outline"
                  className={getStatusColor(client.status)}
                >
                  {getStatusLabel(client.status)}
                </Badge>
              </div>

              {/* Grid de informação resumida no cabeçalho */}
              <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2 text-sm text-muted-foreground">
                {client.phone && (
                  <span className="flex items-center gap-2">
                    <Phone className="h-3.5 w-3.5" /> {client.phone}
                  </span>
                )}
                {client.email && (
                  <span className="flex items-center gap-2">
                    <Mail className="h-3.5 w-3.5" /> {client.email}
                  </span>
                )}
                {client.birth_date && (
                  <span className="flex items-center gap-2">
                    <Calendar className="h-3.5 w-3.5" />
                    {calculateAge(client.birth_date)} anos (
                    {formatDate(client.birth_date)})
                  </span>
                )}
                {client.sex && (
                  <span className="flex items-center gap-2">
                    <User className="h-3.5 w-3.5" /> {getSexLabel(client.sex)}
                  </span>
                )}
                {client.height_cm && (
                  <span className="flex items-center gap-2">
                    <Ruler className="h-3.5 w-3.5" /> {client.height_cm} cm
                  </span>
                )}
                {client.training_modality && (
                  <span className="flex items-center gap-2">
                    <Dumbbell className="h-3.5 w-3.5" />
                    {client.training_modality.charAt(0).toUpperCase() +
                      client.training_modality.slice(1)}
                  </span>
                )}
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Sistema de Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="w-full sm:w-auto">
          <TabsTrigger value="perfil" className="flex items-center gap-1.5">
            <User className="h-3.5 w-3.5" />
            Perfil
          </TabsTrigger>

          <TabsTrigger value="acesso" className="flex items-center gap-1.5">
            <KeyRound className="h-3.5 w-3.5" />
            Acesso
          </TabsTrigger>

          <TabsTrigger value="sessoes" className="flex items-center gap-1.5">
            <Calendar className="h-3.5 w-3.5" />
            Sessões
            {sessions.length > 0 && (
              <span className="ml-1 bg-primary/10 text-primary text-xs rounded-full px-1.5 py-0.5 leading-none">
                {sessions.length}
              </span>
            )}
          </TabsTrigger>
        </TabsList>

        {/* TAB: PERFIL */}
        <TabsContent value="perfil" className="mt-4 space-y-4">
          {/* Informação detalhada do perfil */}
          <Card className="bg-card border-border">
            <CardContent className="p-6 space-y-4">
              <h2 className="text-sm font-medium text-foreground">
                Informação Pessoal
              </h2>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
                {client.objetive && (
                  <div>
                    <p className="text-xs text-muted-foreground mb-0.5">
                      Objectivo
                    </p>
                    <p className="text-foreground">{client.objetive}</p>
                  </div>
                )}
                {client.notes && (
                  <div className="sm:col-span-2">
                    <p className="text-xs text-muted-foreground mb-0.5">
                      Notas
                    </p>
                    <p className="text-foreground whitespace-pre-wrap">
                      {client.notes}
                    </p>
                  </div>
                )}
                {client.emergency_contact_name && (
                  <div>
                    <p className="text-xs text-muted-foreground mb-0.5">
                      Contacto de Emergência
                    </p>
                    <p className="text-foreground">
                      {client.emergency_contact_name}
                    </p>
                  </div>
                )}
                {client.emergency_contact_phone && (
                  <div>
                    <p className="text-xs text-muted-foreground mb-0.5">
                      Telefone de Emergência
                    </p>
                    <p className="text-foreground">
                      {client.emergency_contact_phone}
                    </p>
                  </div>
                )}
                {client.next_assessment_date && (
                  <div>
                    <p className="text-xs text-muted-foreground mb-0.5">
                      Próxima Avaliação
                    </p>
                    <p className="text-foreground">
                      {formatDate(client.next_assessment_date)}
                    </p>
                  </div>
                )}
                {/* Fallback quando não há campos adicionais preenchidos */}
                {!client.objetive &&
                  !client.notes &&
                  !client.emergency_contact_name &&
                  !client.next_assessment_date && (
                    <p className="text-sm text-muted-foreground sm:col-span-2">
                      Sem informação adicional registada.
                    </p>
                  )}
              </div>
            </CardContent>
          </Card>

          {/* Pack ativo — progresso de sessões consumidas */}
          <Card className="bg-card border-border">
            <CardContent className="p-6">
              <h2 className="text-sm font-medium text-foreground mb-4">
                Pack Ativo
              </h2>
              {client.active_pack ? (
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-foreground">
                      {client.active_pack.pack_type_name}
                    </span>
                    <span className="text-sm text-muted-foreground">
                      {client.active_pack.sessions_used} /{' '}
                      {client.active_pack.sessions_total} sessões usadas
                    </span>
                  </div>
                  <Progress value={packProgress} className="h-2" />
                  <p className="text-xs text-muted-foreground">
                    {client.active_pack.sessions_remaining} sessões restantes
                  </p>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Nenhum pack ativo.
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* TAB: ACESSO */}

        <TabsContent value="acesso" className="mt-4">
          <Card className="bg-card border-border">
            <CardContent className="p-6 space-y-6">
              {/* Email de login do cliente */}
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                  Email de Acesso
                </p>
                <div className="flex items-center gap-2 p-3 rounded-md bg-muted">
                  <Mail className="h-4 w-4 text-muted-foreground shrink-0" />
                  <span className="text-sm text-foreground font-mono">
                    {client.email || 'Sem email registado'}
                  </span>
                </div>
              </div>

              <div className="border-t border-border" />

              {/* Secção do link de convite */}
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <KeyRound className="h-4 w-4 text-muted-foreground" />
                  <h3 className="text-sm font-medium text-foreground">
                    Link de Primeiro Acesso
                  </h3>
                </div>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  Gera um link de convite para o cliente definir a sua password
                  e aceder à plataforma. O link é válido por{' '}
                  <strong>7 dias</strong> e só pode ser usado{' '}
                  <strong>uma vez</strong>. Ao gerar um novo link, o anterior é
                  invalidado.
                </p>

                {/* Estado: sem link gerado, mostra botão de geração */}
                {!inviteLink ? (
                  <Button
                    variant="outline"
                    onClick={handleGenerateInvite}
                    disabled={generatingInvite}
                    className="w-full sm:w-auto"
                  >
                    {generatingInvite ? (
                      'A gerar...'
                    ) : (
                      <>
                        <Link className="h-4 w-4 mr-2" />
                        Gerar Link de Convite
                      </>
                    )}
                  </Button>
                ) : (
                  // Estado: link gerado,  mostra o link e as acções
                  <div className="space-y-3">
                    {/* Caixa de exibição do link */}
                    <div className="flex items-center gap-2 p-3 rounded-md bg-muted border border-border">
                      <span className="flex-1 text-xs font-mono text-muted-foreground break-all">
                        {inviteLink}
                      </span>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="shrink-0 h-7 w-7"
                        onClick={handleCopyInvite}
                        title="Copiar link"
                      >
                        <Copy className="h-3.5 w-3.5" />
                      </Button>
                    </div>

                    {/* Acções disponíveis após geração */}
                    <div className="flex flex-wrap gap-2">
                      {/*
                        Botão WhatsApp:
                        Cor oficial #25D366 (verde WhatsApp).
                        wa.me/?text= abre o WhatsApp com a mensagem pré-escrita.
                        noopener,noreferrer: segurança — a nova tab não tem acesso
                        ao objecto window da tab pai.
                      */}
                      <Button
                        onClick={handleShareWhatsApp}
                        size="sm"
                        className="bg-[#25D366] hover:bg-[#1ebe5d] text-white border-0"
                      >
                        <MessageCircle className="h-4 w-4 mr-2" />
                        Partilhar via WhatsApp
                      </Button>

                      {/* Cópia manual — para outros canais (SMS, email, etc.) */}
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={handleCopyInvite}
                      >
                        <Copy className="h-4 w-4 mr-2" />
                        Copiar Link
                      </Button>

                      {/* Regenerar invalida o token anterior no backend */}
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={handleGenerateInvite}
                        disabled={generatingInvite}
                        className="text-muted-foreground"
                      >
                        {generatingInvite ? 'A gerar...' : 'Gerar Novo Link'}
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* TAB: SESSÕES */}
        <TabsContent value="sessoes" className="mt-4">
          <Card className="bg-card border-border">
            <CardContent className="p-6">
              <h2 className="text-sm font-medium text-foreground mb-4">
                Últimas Sessões ({sessions.length})
              </h2>
              {sessions.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  Nenhuma sessão encontrada.
                </p>
              ) : (
                <div className="space-y-2">
                  {/* slice(0, 10) — limita a lista a 10 entradas recentes */}
                  {sessions.slice(0, 10).map((session) => (
                    <div
                      key={session.id}
                      className="flex items-center justify-between py-2 border-b border-border last:border-0"
                    >
                      <div>
                        <p className="text-sm text-foreground">
                          {formatDate(session.starts_at)}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {session.duration_minutes}min
                          {session.location && ` · ${session.location}`}
                        </p>
                      </div>
                      <Badge
                        variant="outline"
                        className={`text-xs ${getStatusColor(session.status)}`}
                      >
                        {getStatusLabel(session.status)}
                      </Badge>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
