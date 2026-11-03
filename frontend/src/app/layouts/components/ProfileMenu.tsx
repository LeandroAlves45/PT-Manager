import { ChevronDown, LogOut, Settings, ShieldCheck, User } from 'lucide-react';
import { useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
// Import direto (e não pelo index da feature): o index traz a página de conta, que é lazy.
import { useAccountQuery } from '@/features/account/api/account';
import { ClientAvatar, usePortalProfileQuery } from '@/features/portal';
import { profileInitials } from '@/features/portal/lib/initials';
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
 * O personal trainer e o administrador veem as iniciais e o nome da conta (`/auth/account`).
 * Sem nome, ou enquanto carrega, aparece um ícone de pessoa: o identificador técnico não diz
 * nada a quem usa a app, por isso nunca é mostrado.
 *
 * Não há sino de notificações: notificações in-app são futuro e um ícone
 * que nunca faz nada é pior do que ícone nenhum.
 */
export function ProfileMenu() {
  const { session, signOut } = useAuth();
  const navigate = useNavigate();
  const isClient = session?.role === 'client';
  const profile = usePortalProfileQuery(isClient).data;
  const accountName = useAccountQuery(session !== null && !isClient).data?.full_name?.trim();
  const displayName = profile?.name ?? (accountName || null);

  if (session === null) return null;

  async function handleSignOut(): Promise<void> {
    await signOut();
    void navigate(AUTH_ROUTES.login, { replace: true });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="gap-2 px-2" aria-label="Menu de perfil">
          {profile !== undefined ? (
            <ClientAvatar
              name={profile.name}
              avatarUrl={profile.avatar_url}
              className="size-7 text-xs"
            />
          ) : (
            <Avatar className="size-7">
              <AvatarFallback className="text-xs">
                {displayName === null ? (
                  <User aria-hidden className="size-4" data-testid="account-avatar-icon" />
                ) : (
                  <span aria-hidden data-testid="account-avatar-initials">
                    {profileInitials(displayName)}
                  </span>
                )}
              </AvatarFallback>
            </Avatar>
          )}
          <ChevronDown aria-hidden className="size-4 opacity-70" />
        </Button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          {displayName === null ? (
            <span className="block text-sm font-medium">{roleLabel(session.role)}</span>
          ) : (
            <>
              <span className="block truncate text-sm font-medium">{displayName}</span>
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
