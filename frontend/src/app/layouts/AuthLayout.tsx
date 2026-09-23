import { Outlet } from 'react-router';

/**
 * Moldura das páginas públicas de autenticação.
 *
 * Deliberadamente sem navegação: quem não tem sessão não tem para onde navegar. A página
 * final de login chega na 6G; a 6C usa a mínima de desenvolvimento.
 */
export function AuthLayout() {
  return (
    <div className="bg-background flex min-h-dvh flex-col items-center justify-center px-4">
      <div className="w-full max-w-sm space-y-6">
        <div className="space-y-1 text-center">
          <p className="font-display text-3xl">PT Manager</p>
          <p className="text-muted-foreground text-sm">Gestão de clientes para personal trainers</p>
        </div>
        <Outlet />
      </div>
    </div>
  );
}
