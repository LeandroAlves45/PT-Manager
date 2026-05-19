/**
 * AdminDashboard.jsx — métricas globais da plataforma.
 *
 * Mostra: total de Personal Trainers, Personal Trainers ativos/em trial,
 * total de clientes, e receita mensal estimada.
 */

import { useEffect, useState, useCallback } from 'react';
import { TrendingUp, UserCheck, Euro } from 'lucide-react';
import { getTrainers, getPlatformMetrics } from '../../api/adminApi';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
} from 'recharts';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '../../components/ui/card';

// Cores consistentes com o tema da app
const CHART_COLORS = {
  primary: 'hsl(var(--primary))',
  active: '#22c55e', // green-500
  trialing: '#3b82f6', // blue-500
  past_due: '#f97316', // orange-500
  cancelled: '#ef4444', // red-500
  expired: '#6b7280', // gray-500
  free: '#94a3b8', // slate-400
  starter: '#3b82f6', // blue-500
  pro: '#8b5cf6', // violet-500
};

// Tooltip personalizado para os gráficos
function CustomTooltip({ active, payload, label }) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border border-border bg-card px-3 py-2 shadow-md text-sm">
      {label && <p className="font-medium text-foreground mb-1">{label}</p>}
      {payload.map((entry, i) => (
        <p
          key={i}
          style={{ color: entry.color || entry.fill }}
          className="text-xs"
        >
          {entry.name}: <span className="font-semibold">{entry.value}</span>
        </p>
      ))}
    </div>
  );
}

export default function AdminDashboard() {
  const [metrics, setMetrics] = useState(null);
  const [trainers, setTrainers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      // Carrega métricas e lista de Personal Trainers em paralelo
      const [metricsResult, trainersResult] = await Promise.allSettled([
        getPlatformMetrics(),
        getTrainers(),
      ]);

      if (metricsResult.status === 'fulfilled') {
        setMetrics(metricsResult.value);
      } else {
        // Métricas falharam mas não bloqueamos o resto do dashboard
        console.error(
          '[AdminDashboard] Erro nas métricas:',
          metricsResult.reason
        );
        setError('Erro ao carregar métricas da plataforma.');
      }

      if (trainersResult.status === 'fulfilled') {
        setTrainers(trainersResult.value);
      } else {
        // Gráficos não aparecem mas os cards de métricas ainda funcionam
        console.error(
          '[AdminDashboard] Erro na lista de trainers:',
          trainersResult.reason
        );
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-64">
        <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  // Contagem por tier de subscrição
  const tierCounts = trainers.reduce((acc, t) => {
    const tier = t.subscription_tier || 'free';
    acc[tier] = (acc[tier] || 0) + 1;
    return acc;
  }, {});

  const tierData = [
    { name: 'FREE', value: tierCounts['free'] || 0, fill: CHART_COLORS.free },
    {
      name: 'STARTER',
      value: tierCounts['starter'] || 0,
      fill: CHART_COLORS.starter,
    },
    { name: 'PRO', value: tierCounts['pro'] || 0, fill: CHART_COLORS.pro },
  ].filter((d) => d.value > 0);

  // Contagem por status de subscrição
  const statusCounts = trainers.reduce((acc, t) => {
    const status = t.subscription_status || 'unknown';
    acc[status] = (acc[status] || 0) + 1;
    return acc;
  }, {});

  const statusData = [
    {
      name: 'Ativo',
      value: statusCounts['active'] || 0,
      fill: CHART_COLORS.active,
    },
    {
      name: 'Trial',
      value: statusCounts['trialing'] || 0,
      fill: CHART_COLORS.trialing,
    },
    {
      name: 'Em falta',
      value: statusCounts['past_due'] || 0,
      fill: CHART_COLORS.past_due,
    },
    {
      name: 'Cancelado',
      value: statusCounts['cancelled'] || 0,
      fill: CHART_COLORS.cancelled,
    },
    {
      name: 'Trial expirado',
      value: statusCounts['trial_expired'] || 0,
      fill: CHART_COLORS.expired,
    },
  ].filter((d) => d.value > 0);

  const cards = [
    {
      title: 'Total de Personal Trainers',
      value: metrics?.total_trainers ?? 0,
      icon: UserCheck,
      description: 'Contas registadas na plataforma',
    },
    {
      title: 'Personal Trainers Ativos',
      value: metrics?.active_trainers ?? 0,
      icon: UserCheck,
      description: `${metrics?.trialing_trainers ?? 0} em trial`,
    },
    {
      title: 'Total de Clientes',
      value: metrics?.total_clients ?? 0,
      icon: TrendingUp,
      description: 'Clientes ativos na plataforma',
    },
    {
      title: 'Receita Estimada',
      value: `€${(metrics?.estimated_monthly_revenue_eur ?? 0).toLocaleString('pt-PT')}`,
      icon: Euro,
      description: 'Por mês (subscrições ativas)',
    },
  ];

  const hasChartData = trainers.length > 0;

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Dashboard</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Visão geral da plataforma PT Manager
        </p>
      </div>

      {/* Cards de métricas */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {cards.map((card) => (
          <Card key={card.title} className="border-border bg-card">
            <CardHeader className="flex flex-row items-center justify-center pb-4">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                {card.title}
              </CardTitle>
              <card.icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <p className="text-2xl font-bold text-foreground">{card.value}</p>
              <p className="text-xs text-muted-foreground mt-1">
                {card.description}
              </p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Gráficos — só mostra se houver dados */}
      {hasChartData ? (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Gráfico 1 - Trainer por tier de subscrição */}
          <Card className="border-border bg-card">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                Personal Trainers por Tier de Subscrição
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={220}>
                <BarChart
                  data={tierData}
                  margin={{ top: 5, right: 10, left: -20, bottom: 5 }}
                >
                  <CartesianGrid
                    strokeDasharray="3 3"
                    stroke="hsl(var(--border))"
                  />
                  <XAxis
                    dataKey="name"
                    tick={{
                      fill: 'hsl(var(--muted-foreground))',
                      fontSize: 12,
                    }}
                    axisLine={false}
                    tickLine={false}
                  />
                  <YAxis
                    allowDecimals={false}
                    tick={{
                      fill: 'hsl(var(--muted-foreground))',
                      fontSize: 12,
                    }}
                    axisLine={false}
                    tickLine={false}
                  />
                  <Tooltip content={<CustomTooltip />} />
                  <Bar
                    dataKey="value"
                    name="Personal Trainers"
                    radius={[4, 4, 0, 0]}
                  >
                    {tierData.map((entry, index) => (
                      <Cell key={index} fill={entry.fill} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>

          {/* Gráfico 2 - Personal Trainer por status  */}
          <Card className="border-border bg-card">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                Personal Trainers por Status de Subscrição
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={220}>
                <PieChart>
                  <Pie
                    data={statusData}
                    cx="50%"
                    cy="45%"
                    outerRadius={75}
                    dataKey="value"
                    nameKey="name"
                    label={({ name, value }) => `${name}: ${value}`}
                    labelLine={false}
                  >
                    {statusData.map((entry, index) => (
                      <Cell key={index} fill={entry.fill} />
                    ))}
                  </Pie>
                  <Tooltip content={<CustomTooltip />} />
                  <Legend
                    wrapperStyle={{
                      fontSize: '11px',
                      color: 'hsl(var(--muted-foreground))',
                    }}
                  />
                </PieChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </div>
      ) : (
        <div className="rounded-lg border border-dashed border-border bg-card/50 p-8 flex flex-col items-center justify-center gap-2 text-center">
          <TrendingUp className="h-8 w-8 text-muted-foreground" />
          <p className="text-sm font-medium text-muted-foreground">
            Ainda não há Personal Trainers registados. Os gráficos aparecerão
            aqui assim que houver dados para mostrar.
          </p>
          <p className="text-xs text-muted-foreground/70">
            Os gráficos aparecem quando existirem Personal Trainers registados.
          </p>
        </div>
      )}
    </div>
  );
}
