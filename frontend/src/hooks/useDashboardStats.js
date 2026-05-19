import { useState, useEffect } from 'react';
import { useClients } from './useClients';
import { useSessions } from './useSessions';
import { getPackTypes } from '@/api/packTypesApi';

export const useDashboardStats = () => {
  const { clients } = useClients();
  const { sessions } = useSessions();
  const [packTypes, setPackTypes] = useState([]);

  useEffect(() => {
    const fetchPackTypes = async () => {
      try {
        const data = await getPackTypes();
        setPackTypes(data);
      } catch (error) {
        console.error('Erro ao carregar tipos de packs:', error);
      }
    };

    fetchPackTypes();
  }, []);

  //Estatísticas calculadas
  const stats = {
    //Clientes
    totalClients: clients.length,
    activeClients: clients.filter((c) => c.status === 'active').length,
    clientsWithActivePack: clients.filter((c) => c.active_pack).length,

    //Sessões
    totalSessions: sessions.length,
    todaySessions: sessions.filter(
      (s) => new Date(s.starts_at).toDateString() === new Date().toDateString()
    ).length,

    //Packs
    activePacks: clients.filter((c) => c.active_pack).length,

    //Clientes em risco (sem sessão agendada e pack a acabar)
    clientsAtRisk: clients.filter((c) => {
      if (!c.active_pack) return true; // Sem pack ativo já é um risco
      if (c.active_pack.sessions_remaining <= 2) return true; // Pack quase a acabar
      return false;
    }),

    //Próximas sessões - usadas em UpcomingSessions e AlertsPanel
    upcomingSessions: sessions
      .filter(
        (s) => s.status === 'scheduled' && new Date(s.starts_at) > new Date()
      )
      .sort((a, b) => new Date(a.starts_at) - new Date(b.starts_at)),

    //Sessões da semana
    weekSessions: sessions.filter((s) => {
      const now = new Date();
      const weekStart = new Date(now);
      weekStart.setDate(now.getDate() - now.getDay()); //recua até domingo
      weekStart.setHours(0, 0, 0, 0); // Início do dia
      const weekEnd = new Date(weekStart);
      weekEnd.setDate(weekStart.getDate() + 7);
      const sessionDate = new Date(s.starts_at);
      return sessionDate >= weekStart && sessionDate < weekEnd;
    }),
  };

  return stats;
};
