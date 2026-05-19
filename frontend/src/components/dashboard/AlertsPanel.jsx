import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, Package, Calendar } from 'lucide-react';
import { formatDateTime, diffInHours } from '@/utils/formatters';

export default function AlertsPanel({ clientsAtRisk, upcomingSessions }) {
  const alerts = [];

  //Alertas de clientes sem packs
  clientsAtRisk.forEach((client) => {
    if (!client.activePack) {
      alerts.push({
        type: 'danger',
        icon: Package,
        title: 'Cliente sem pack ativo',
        description: `${client.full_name} não tem um pack ativo.`,
        action: 'Atribuir pack',
      });
    } else if (client.active_pack.sessions_remaining <= 2) {
      alerts.push({
        type: 'warning',
        icon: Package,
        title: 'Pack quase esgotado',
        description: `${client.full_name} tem apenas ${client.active_pack.sessions_remaining} sessões restantes.`,
        action: 'Renovar pack',
      });
    }
  });

  // Alertas de sessões próximas
  const today = new Date();
  upcomingSessions
    .filter((session) => {
      const sessionDate = new Date(session.starts_at);
      const timeHours = diffInHours(today, sessionDate);
      return timeHours <= 24 && timeHours > 0; // Próximas 24 horas
    })
    .forEach((session) => {
      const clientName = session.client?.full_name ?? 'cliente';
      alerts.push({
        type: 'info',
        icon: Calendar,
        title: 'Sessão próxima',
        description: `Sessão de ${clientName} agendada para ${formatDateTime(session.starts_at)}.`,
        action: 'Ver detalhes',
      });
    });

  if (alerts.length === 0) {
    return (
      <Card className="bg-card border-border">
        <CardHeader>
          <CardTitle className="text-foreground">Alertas</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-center text-muted-foreground py-8">
            Sem alertas no momento
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-card border-border">
      <CardHeader>
        <CardTitle className="text-foreground flex items-center gap-2">
          <AlertCircle className="h-5 w-5" />
          Alertas
          <Badge variant="outline" className="ml-auto">
            {alerts.length}
          </Badge>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          {alerts.slice(0, 5).map((alert, index) => {
            const Icon = alert.icon;
            const colorClass =
              alert.type === 'danger'
                ? 'text-destructive'
                : alert.type === 'warning'
                  ? 'text-orange-500'
                  : 'text-primary';

            return (
              <div
                key={index}
                className="flex items-start gap-3 p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors"
              >
                <Icon className={`h-5 w-5 mt-0.5 ${colorClass}`} />
                <div className="flex-1">
                  <p className="text-sm font-medium text-foreground">
                    {alert.title}
                  </p>
                  <p className="text-xs text-muted-foreground mt-1">
                    {alert.description}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}
