import { vi } from 'vitest';

import { resetGoogleIdentityLoader } from '@/features/auth/lib/google-identity';

/**
 * `window.google` falso para os testes do Google Identity Services.
 *
 * Com a API já presente o carregador resolve sem pedir o script. O teste dispara a escolha de
 * uma conta com `pick(credential)`, que chama o `callback` registado no último `initialize`.
 */
export interface GoogleFake {
  readonly initialize: ReturnType<typeof vi.fn<(configuration: GoogleIdConfiguration) => void>>;
  readonly renderButton: ReturnType<
    typeof vi.fn<(parent: HTMLElement, options: GoogleButtonConfiguration) => void>
  >;
  /** Simula o utilizador a escolher uma conta Google. */
  readonly pick: (credential: string) => void;
}

/** Instala o `window.google` falso; `setup.ts` desfaz o `stubGlobal`. */
export function installGoogleFake(): GoogleFake {
  resetGoogleIdentityLoader();

  let callback: ((response: GoogleCredentialResponse) => void) | null = null;

  const initialize = vi.fn((configuration: GoogleIdConfiguration) => {
    callback = configuration.callback;
  });
  const renderButton = vi.fn((parent: HTMLElement) => {
    const button = document.createElement('div');
    button.setAttribute('data-testid', 'google-button');
    parent.appendChild(button);
  });

  vi.stubGlobal('google', { accounts: { id: { initialize, renderButton } } });

  return {
    initialize,
    renderButton,
    pick: (credential) => {
      if (callback === null) throw new Error('initialize ainda não foi chamado.');
      callback({ credential });
    },
  };
}
