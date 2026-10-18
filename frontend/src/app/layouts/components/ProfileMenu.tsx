import { ChevronDown, LogOut, Settings, ShieldCheck, User } from 'lucide-react';
import { useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { ClientAvatar, usePortalProfileQuery } from '@/features/portal';
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
import { homeRouteFor } from '@/shared/api/session';
import { AUTH_ROUTES } from '@/shared/config/auth-routes';
import { roleLabel } from '@/shared/config/navigation';

/**
 * Menu de perfil da topbar.
 *
 * O cliente vê a sua fotografia (ou as iniciais do nome) e o nome, lidos do perfil do portal.
 * O personal trainer e o administrador não têm perfil com nome na API: continuam com as duas
 * primeiras letras do identificador, e nenhum pedido é feito por eles.
 *
 * Não há sino de notificações: notificações in-app são futuro e um ícone
 * que nunca faz nada é pior do que ícone nenhum.
 */
export function ProfileMenu() {
  const { session, signOut } = useAuth();
  const navigate = useNavigate();
  const profile = usePortalProfileQuery(session?.role === 'client').data;

  if (session === null) return null;

  async function handleSignOut(): Promise<void> {
    await signOut();
    void navigate(AUTH_ROUTES.login, { replace: true });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="gap-2 px-2" aria-label="Menu de perfil">
          {profile === undefined ? (
            <Avatar className="size-7">
              <AvatarFallback className="text-xs">
                {session.userId.slice(0, 2).toUpperCase()}
              </AvatarFallback>
            </Avatar>
          ) : (
            <ClientAvatar
              name={profile.name}
              avatarUrl={profile.avatar_url}
              className="size-7 text-xs"
            />
          )}
          <ChevronDown aria-hidden className="size-4 opacity-70" />
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          {profile === undefined ? (
            <>
              <span className="block text-sm font-medium">{roleLabel(session.role)}</span>
              <span className="tabular text-muted-foreground block text-xs">{session.userId}</span>
            </>
          ) : (
            <>
              <span className="block truncate text-sm font-medium">{profile.name}</span>
              <span className="text-muted-foreground block text-xs">{roleLabel(session.role)}</span>
            </>
          )}
        </DropdownMenuLabel>

        <DropdownMenuSeparator />

        {/* Só o cliente tem página de perfil; o personal trainer tem Definições (marca,
            contactos) e o superuser não tem rota de perfil. */}
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

        {/* Os três papéis têm conta e segurança, dentro do layout da sua área. */}
        <DropdownMenuItem onSelect={() => void navigate(`${homeRouteFor(session.role)}/account`)}>
          <ShieldCheck aria-hidden />
          Conta e segurança
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        <DropdownMenuItem onSelect={() => void handleSignOut()}>
          <LogOut aria-hidden />
          Terminar sessão
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
