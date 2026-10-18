import { Outlet } from 'react-router';

import { ThemeToggle } from '@/app/components/ThemeToggle';

/**
 * Moldura das páginas públicas de autenticação.
 *
 * Mobile-first (cartão de largura total aos 375 px, até `max-w-md` a partir de `sm`), com a
 * marca em cima e o alternador de tema no canto: quem ainda não entrou também escolhe o tema.
 * Deliberadamente sem navegação: quem não tem sessão não tem para onde navegar. Cada página
 * traz o seu `<h1>`; o `<main>` é o único marco da moldura.
 */
export function AuthLayout() {
  return (
    <div className="bg-background flex min-h-dvh flex-col items-center justify-center px-4 py-10">
      <div className="absolute top-3 right-3">
        <ThemeToggle />
      </div>

      <main className="w-full max-w-sm space-y-6 sm:max-w-md">
        <div className="space-y-1 text-center">
          <p className="font-display text-3xl">PT Manager</p>
          <p className="text-muted-foreground text-sm">Gestão de clientes para personal trainers</p>
        </div>

        <div className="border-border bg-card rounded-xl border p-6 shadow-xs">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
