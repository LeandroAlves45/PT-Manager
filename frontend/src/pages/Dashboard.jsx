import { useDashboardStats } from '@/hooks/useDashboardStats';
import StatsCards from '@/components/dashboard/StatsCards';
import WeeklyChart from '@/components/dashboard/WeeklyChart';
import AlertsPanel from '@/components/dashboard/AlertsPanel';
import ClientsAtRisk from '@/components/dashboard/ClientsAtRisk';
import UpcomingSessions from '@/components/dashboard/UpcomingSessions';

/**
 * Página principal - Dashboard.
 *
 * Busca dados de clientes e sessões via hooks e calcula as estatísticas
 * para passar aos componentes filhos.
 *
 * Fluxo de dados:
 * API → hooks (useClients, useSessions) → Dashboard → componentes filhos (via props)
 */

export default function Dashboard() {
  const stats = useDashboardStats();

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Dashboard</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Visão geral da sua atividade
        </p>
      </div>

      {/* Cards de Estatísticas */}
      <StatsCards stats={stats} />

      {/* Gráficos e Alertas */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <WeeklyChart sessions={stats.weekSessions} />
        <AlertsPanel
          clientsAtRisk={stats.clientsAtRisk}
          upcomingSessions={stats.upcomingSessions}
        />
      </div>

      {/* Clientes em Risco e Próximas Sessões */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <ClientsAtRisk clients={stats.clientsAtRisk} />
        <UpcomingSessions sessions={stats.upcomingSessions} />
      </div>
    </div>
  );
}
