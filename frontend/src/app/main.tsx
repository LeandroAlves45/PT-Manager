import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router';

import { AuthProvider } from '@/app/providers/AuthProvider';
import { QueryProvider } from '@/app/providers/QueryProvider';
import { ThemeContext } from '@/app/providers/theme-context';
import { ThemeProvider } from '@/app/providers/ThemeProvider';
import { router } from '@/app/router';
import { Toaster } from '@/shared/components/ui/sonner';
import { Tooltip as TooltipPrimitive } from 'radix-ui';
import { invariant } from '@/shared/lib/invariant';

import '@/shared/styles/globals.css';

const container = document.getElementById('root');
invariant(container, 'Elemento #root não encontrado no index.html.');

/**
 * Ponto de entrada.
 *
 * Ordem dos providers, de fora para dentro:
 * 1. tema — aplica a classe `dark` antes de qualquer componente pintar;
 * 2. cache de dados — o `AuthProvider` já faz pedidos e precisa dela montada;
 * 3. sessão — restaura o utilizador;
 * 4. tooltips e rotas.
 */
createRoot(container).render(
  <StrictMode>
    <ThemeProvider>
      <QueryProvider>
        <AuthProvider>
          <TooltipPrimitive.Provider delayDuration={200}>
            <RouterProvider router={router} />
            <ThemeContext.Consumer>
              {(theme) => {
                invariant(theme, 'Toaster fora do ThemeProvider.');
                return <Toaster theme={theme.resolved} position="bottom-right" />;
              }}
            </ThemeContext.Consumer>
          </TooltipPrimitive.Provider>
        </AuthProvider>
      </QueryProvider>
    </ThemeProvider>
  </StrictMode>
);
