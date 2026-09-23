import { ChevronDown, LogOut, Settings, User } from 'lucide-react';
import { useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { Avatar, AvatarFallback } from '@/shared/components/ui/avatar';
import { Button } from '@/shared/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu';
import { roleLabel } from '@/shared/config/navigation';

/**
 * Menu de perfil da topbar.
 *
 * Não há sino de notificações: notificações in-app são futuro e um ícone
 * que nunca faz nada é pior do que ícone nenhum.
 */
export function ProfileMenu() {
  const { session, signOut } = useAuth();
  const navigate = useNavigate();

  if (session === null) return null;

  async function handleSignOut(): Promise<void> {
    await signOut();
    void navigate('/auth/login', { replace: true });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="gap-2 px-2" aria-label="Menu de perfil">
          <Avatar className="size-7">
            <AvatarFallback className="text-xs">
              {session.userId.slice(0, 2).toUpperCase()}
            </AvatarFallback>
          </Avatar>
          <ChevronDown aria-hidden className="size-4 opacity-70" />
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <span className="block text-sm font-medium">{roleLabel(session.role)}</span>
          <span className="tabular text-muted-foreground block text-xs">{session.userId}</span>
        </DropdownMenuLabel>

        <DropdownMenuSeparator />

        {/* Só o cliente tem página de perfil; o personal trainer gere a conta em Definições e o
            superuser não tem rota de perfil. */}
        {session.role === 'client' && (
          <DropdownMenuItem onSelect={() => void navigate('/portal/profile')}>
            <User aria-hidden />O meu perfil
          </DropdownMenuItem>
        )}

        {session.role === 'trainer' && (
          <DropdownMenuItem onSelect={() => void navigate('/trainer/settings')}>
            <Settings aria-hidden />
            Definições
          </DropdownMenuItem>
        )}

        <DropdownMenuSeparator />

        <DropdownMenuItem onSelect={() => void handleSignOut()}>
          <LogOut aria-hidden />
          Terminar sessão
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
