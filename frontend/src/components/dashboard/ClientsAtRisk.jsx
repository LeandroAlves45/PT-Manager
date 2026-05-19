import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { AlertTriangle } from 'lucide-react';
import { getInitials } from '@/lib/helpers';

export default function ClientsAtRisk({ clients }) {
  if (clients.length === 0) {
    return (
      <Card className="bg-card border-border">
        <CardHeader>
          <CardTitle className="text-foreground">Clientes em Risco</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-center text-muted-foreground py-8">
            Nenhum cliente em risco no momento
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-card border-border">
      <CardHeader>
        <CardTitle className="text-foreground flex items-center gap-2">
          <AlertTriangle className="h-5 w-5 text-orange-500" />
          Clientes em Risco
          <Badge variant="outline" className="ml-auto">
            {clients.length}
          </Badge>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          {clients.slice(0, 5).map((client) => (
            <div
              key={client.id}
              className="flex items-center gap-3 p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors"
            >
              <Avatar className="h-10 w-10">
                <AvatarFallback className="bg-primary/10 text-primary text-sm font-semibold">
                  {getInitials(client.full_name)}
                </AvatarFallback>
              </Avatar>
              <div className="flex-1">
                <p className="text-sm font-medium text-foreground">
                  {client.full_name}
                </p>
                <p className="text-xs text-muted-foreground">
                  {!client.active_pack
                    ? 'Sem pack ativo'
                    : `${client.active_pack.sessions_remaining} sessões restantes`}
                </p>
              </div>
              <Badge
                variant="outline"
                className={
                  !client.active_pack
                    ? 'border-destructive text-destructive'
                    : 'border-orange-500 text-orange-500'
                }
              >
                {!client.active_pack ? 'Sem pack' : 'A acabar'}
              </Badge>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
