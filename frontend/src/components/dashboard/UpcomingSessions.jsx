import { CalendarDays, Clock, MapPin } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { getInitials, formatDateTime, getStatusColor } from '@/lib/helpers';

/**
 * Lista as próximas sessões do cliente
 * Mostra até 5 sessões
 *
 * @param {Array} sessions - Array de objetos de sessões, cada objeto deve conter:
 */
function UpcomingSessions({ sessions = [] }) {
  //Filtra as sessões para mostrar apenas as próximas 5, ordenadas por data
  const upcomingSessions = sessions
    .filter(
      (s) => s.status === 'scheduled' && new Date(s.starts_at) >= new Date()
    )
    .sort((a, b) => new Date(a.starts_at) - new Date(b.starts_at))
    .slice(0, 5);

  return (
    <Card className="bg-card border-border">
      <CardContent className="p-5">
        <h3 className="text-sm font-medium text-foreground mb-4">
          Próximas Sessões
        </h3>

        {upcomingSessions.length === 0 ? (
          <p className="text-sm text-muted-foreground py-4 text-center">
            Sem sessões agendadas.
          </p>
        ) : (
          <div className="flex flex-col gap-3">
            {upcomingSessions.map((session) => (
              <div
                key={session.id}
                className="flex items-center gap-3 rounded-lg p-2.5 hover:bg-accent transition-colors"
              >
                {/* Avatar com iniciais do cliente */}
                <Avatar>
                  <AvatarFallback className="bg-primary/10 text-primary text-xs font-semibold">
                    {getInitials(session.client_name || '??')}
                  </AvatarFallback>
                </Avatar>

                {/* Detalhes da sessão */}
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-foreground truncate">
                    {session.client_name || 'Cliente'}
                  </p>
                  <div className="flex items-center gap-3 text-xs text-muted-foreground">
                    <CalendarDays className="w-3 h-3" />
                    <span>{formatDateTime(session.starts_at)}</span>
                    <span className="flex items-center gap-1">
                      <Clock className="w-3 h-3" />
                      {session.duration_minutes} min
                    </span>
                    {session.location && (
                      <span className="flex items-center gap-1">
                        <MapPin className="w-3 h-3" />
                        {session.location}
                      </span>
                    )}
                  </div>
                </div>

                {/*Badge de status da sessão */}
                <Badge
                  variant="outline"
                  className={`text-xs ${getStatusColor(session.status)}`}
                >
                  Agendada
                </Badge>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

export default UpcomingSessions;
