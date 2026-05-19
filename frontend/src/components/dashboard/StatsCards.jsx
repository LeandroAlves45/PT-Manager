import { Users, CalendarDays, Package, TrendingUp } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

/**
 * Componente de cartões de estatísticas para o dashboard
 *
 * Recebe dados via props ou busca via API (ex: total clientes, sessões esta semana, planos ativos, progresso médio)
 * Mostra: total de clientes, sessões esta semana, planos ativos, progresso médio
 *
 * Props:
 * @param {number} totalClients - Total de clientes
 * @param {number} sessionsToday - Total de sessões esta semana
 * @param {number} activePacks - Total de packs ativos
 * @param {number} totalSessions - Progresso médio do mês
 */

export default function StatsCards({ stats }) {
  const cards = [
    {
      title: 'Clientes Ativos',
      value: stats.activeClients,
      total: stats.totalClients,
      icon: Users,
      color: 'text-blue-500',
      bgColor: 'bg-blue-500/10',
    },
    {
      title: 'Sessões Hoje',
      value: stats.todaySessions,
      icon: CalendarDays,
      color: 'text-green-500',
      bgColor: 'bg-green-500/10',
    },
    {
      title: 'Packs Ativos',
      value: stats.activePacks,
      icon: Package,
      color: 'text-purple-500',
      bgColor: 'bg-purple-500/10',
    },
    {
      title: 'Sessões Mês',
      value: stats.weekSessions.length,
      icon: TrendingUp,
      color: 'text-orange-500',
      bgColor: 'bg-orange-500/10',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      {cards.map((card, index) => {
        const Icon = card.icon;
        return (
          <Card key={index} className="bg-card border-border">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground mb-1">
                    {card.title}
                  </p>
                  <div className="flex items-baseline gap-2">
                    <h3 className="text-2xl font-semibold text-foreground">
                      {card.value}
                    </h3>
                    {card.total && (
                      <span className="text-sm text-muted-foreground">
                        / {card.total}
                      </span>
                    )}
                  </div>
                </div>
                <div className={`p-3 rounded-lg ${card.bgColor}`}>
                  <Icon className={`h-6 w-6 ${card.color}`} />
                </div>
              </div>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}
