/**
 * ClientDashboard.jsx — dashboard do portal do cliente.
 *
 * Mostra: check-ins pendentes, nome do plano ativo, e acesso rápido
 * às secções principais. É a primeira página após o login do cliente.
 */

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  ClipboardList,
  Dumbbell,
  UtensilsCrossed,
  ChevronRight,
  UserRound,
} from 'lucide-react';
import { getMyCheckIns, getMyTrainingPlan } from '@/api/clientPortalApi';
import { useAuth } from '@/context/useAuth';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

export default function ClientDashboard() {
  const { user } = useAuth();
  const [pendingCount, setPendingCount] = useState(0);
  const [planName, setPlanName] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        const [checkins, planData] = await Promise.all([
          getMyCheckIns(),
          getMyTrainingPlan(),
        ]);
        // Conta apenas os check-ins pendentes
        setPendingCount(checkins.filter((c) => c.status === 'pending').length);
        setPlanName(planData?.plan?.name ?? null);
      } catch {
        // falha silenciosa — o dashboard é apenas um resumo
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  const quickLinks = [
    {
      label: 'Plano de Treino',
      description: planName ?? 'Ver o teu plano de treino',
      href: '/cliente/plano',
      icon: Dumbbell,
      badge: null,
    },
    {
      label: 'Nutrição',
      description: 'Consultar o teu plano alimentar',
      href: '/cliente/nutricao',
      icon: UtensilsCrossed,
      badge: null,
    },
    {
      label: 'Check-Ins',
      description: 'Responder ao teu Personal Trainer',
      href: '/cliente/checkins',
      icon: ClipboardList,
      badge: pendingCount > 0 ? pendingCount : null,
    },
    {
      label: 'Perfil',
      description: 'Consultar os teus dados pessoais',
      href: '/cliente/perfil',
      icon: UserRound,
      badge: null,
    },
  ];

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Saudação */}
      <div>
        <h1 className="text-2xl font-semibold text-foreground">
          Olá, {user?.full_name.split(' ')[0]} 👋
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Aqui está o teu resumo de hoje.
        </p>
      </div>

      {/* Alerta de check-ins pendentes */}
      {!loading && pendingCount > 0 && (
        <Link to="/cliente/checkins">
          <div className="rounded-lg bg-primary/10 border border-primary/20 p-4 flex items-center justify-between hover:bg-primary/15 transition-colors">
            <div className="flex items-center gap-3">
              <ClipboardList className="h-5 w-5 text-primary shrink-0" />
              <div>
                <p className="text-sm font-medium text-primary">
                  {pendingCount} check-in{pendingCount !== 1 ? 's' : ''}{' '}
                  pendente{pendingCount !== 1 ? 's' : ''}
                </p>
                <p className="text-xs text-muted-foreground">
                  O teu Personal Trainer está à espera da tua resposta.
                </p>
              </div>
            </div>
            <ChevronRight className="h-4 w-4 text-primary" />
          </div>
        </Link>
      )}

      {/* Cards de acesso rápido*/}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
        {quickLinks.map((item) => (
          <Link key={item.href} to={item.href}>
            <Card className="border-border bg-card hover:bg-muted/30 transition-colors cursor-pointer h-full">
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <div className="h-9 w-9 rounded-lg bg-primary/10 flex items-center justify-center">
                    <item.icon className="h-5 w-5 text-primary" />
                  </div>
                  {item.badge && <Badge variant="default">{item.badge}</Badge>}
                </div>
              </CardHeader>
              <CardContent>
                <p className="font-medium text-foreground">{item.label}</p>
                <p className="text-xs text-muted-foreground mt-0.5 line-clamp-1">
                  {loading ? '...' : item.description}
                </p>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
