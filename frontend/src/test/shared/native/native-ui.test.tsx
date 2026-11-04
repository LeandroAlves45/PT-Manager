import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ExternalVideoLink } from '@/features/exercise-video/components/ExternalVideoLink';
import { OfflineNotice } from '@/shared/native/OfflineNotice';
import { installGoogleFake } from '@/test/google-fake';
import { accountResponse } from '@/test/msw/auth-fixtures';
import { API } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  nativeSessionResponse,
  resetNativeFake,
  storedTokens,
  storeTokens,
} from '@/test/native-fakes';
import { renderApp } from '@/test/render';

const fake = vi.hoisted(() => ({
  native: true,
  store: new Map<string, string>(),
  failRead: false,
  lastAccess: undefined as number | undefined,
  opened: [] as string[],
}));

vi.mock('@/shared/native/platform', () => ({ isNativeApp: () => fake.native }));
vi.mock('@aparajita/capacitor-secure-storage', async () =>
  (await import('@/test/native-fakes')).secureStorageModule(fake)
);
vi.mock('@capacitor/browser', () => ({
  Browser: {
    open: (options: { url: string }) => {
      fake.opened.push(options.url);
      return Promise.resolve();
    },
  },
}));

const NATIVE = `${API}/native/auth`;

/** Sessão restaurada na app a partir do par guardado no armazenamento seguro. */
function restoreNative(role: 'client' | 'trainer' | 'superuser') {
  storeTokens(fake, 'refresh-1', 'csrf-1');
  server.use(
    http.post(`${NATIVE}/refresh`, () =>
      HttpResponse.json(
        nativeSessionResponse({
          role,
          trainer_id: role === 'superuser' ? null : '22222222-2222-2222-2222-222222222222',
          refresh_token: 'refresh-2',
          csrf_token: 'csrf-2',
        })
      )
    )
  );
}

beforeEach(() => resetNativeFake(fake));

describe('login in the app', () => {
  it('shows only email and password: no Google, no sign-up, no email confirmation', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'client-1');
    installGoogleFake();
    renderApp({ initialEntries: ['/entrar'] });

    await screen.findByRole('button', { name: 'Entrar' });

    expect(screen.queryByTestId('google-button')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Criar conta' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('link', { name: 'Não recebeste o email de confirmação?' })
    ).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Esqueceste a palavra-passe?' })).toBeInTheDocument();
  });

  it('signs in through the native route and keeps the pair in secure storage', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(`${NATIVE}/login`, async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json(nativeSessionResponse());
      })
    );
    const { router } = renderApp({ initialEntries: ['/entrar'] });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText('Email'), 'ana@ptmanager.test');
    await user.type(screen.getByLabelText('Palavra-passe'), 'Segura!Password1');
    await user.click(screen.getByRole('button', { name: 'Entrar' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
    expect(bodies).toEqual([{ email: 'ana@ptmanager.test', password: 'Segura!Password1' }]);
    expect(storedTokens(fake)).toEqual({ refreshToken: 'refresh-1', csrfToken: 'csrf-1' });
  });
});

describe('roles in the app', () => {
  it('opens the client portal from the stored session', async () => {
    restoreNative('client');
    const { router } = renderApp({ initialEntries: ['/portal'] });

    expect(await screen.findByTestId('portal-header')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/portal');
    expect(storedTokens(fake)).toEqual({ refreshToken: 'refresh-2', csrfToken: 'csrf-2' });
  });

  it.each([
    ['trainer', '/trainer'],
    ['superuser', '/admin'],
  ] as const)(
    'tells a %s the app is for clients, without billing, and signs out through the native route',
    async (role, home) => {
      restoreNative(role);
      const logouts: unknown[] = [];
      server.use(
        http.post(`${NATIVE}/logout`, async ({ request }) => {
          logouts.push(await request.json());
          return new HttpResponse(null, { status: 204 });
        })
      );
      const { router } = renderApp({ initialEntries: [home] });

      expect(
        await screen.findByRole('heading', { name: 'Esta app é para clientes' })
      ).toBeInTheDocument();
      expect(screen.queryByText(/Subscrição/)).not.toBeInTheDocument();

      await userEvent.setup().click(screen.getByRole('button', { name: 'Terminar sessão' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
      expect(logouts).toEqual([{ refresh_token: 'refresh-2', csrf_token: 'csrf-2' }]);
      expect(storedTokens(fake)).toBeNull();
    }
  );
});

describe('account deletion in the app', () => {
  it('forgets the stored pair after the account is deleted', async () => {
    restoreNative('client');
    server.use(
      http.get(`${API}/auth/account`, () => HttpResponse.json(accountResponse({ role: 'client' }))),
      http.post(`${API}/portal/my-account/deletion`, () => new HttpResponse(null, { status: 204 }))
    );
    const { router } = renderApp({ initialEntries: ['/portal/account'] });
    const user = userEvent.setup();
    const card = within(
      (await screen.findByRole('heading', { name: 'Eliminar conta', level: 2 })).closest('section')!
    );

    await user.type(card.getByLabelText('Palavra-passe atual'), 'Atual!Password1');
    await user.type(card.getByLabelText('Escreve ELIMINAR para confirmar'), 'ELIMINAR');
    await user.click(card.getByRole('button', { name: 'Eliminar conta' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
    await waitFor(() => expect(storedTokens(fake)).toBeNull());
  });
});

describe('ExternalVideoLink in the app', () => {
  it('opens the video in the system browser instead of a new tab', async () => {
    fake.opened.length = 0;
    render(<ExternalVideoLink url="https://www.youtube.com/watch?v=dQw4w9WgXcQ" />);

    const link = screen.getByRole('link', { name: /Ver no YouTube/ });
    const notPrevented = fireEvent.click(link);

    expect(notPrevented).toBe(false);
    await waitFor(() =>
      expect(fake.opened).toEqual(['https://www.youtube.com/watch?v=dQw4w9WgXcQ'])
    );
  });
});

describe('OfflineNotice', () => {
  it('appears while the phone is offline and goes away when the connection returns', () => {
    const onLine = vi.spyOn(window.navigator, 'onLine', 'get').mockReturnValue(true);
    render(<OfflineNotice />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    onLine.mockReturnValue(false);
    act(() => {
      window.dispatchEvent(new Event('offline'));
    });
    expect(screen.getByRole('status')).toHaveTextContent('Sem ligação à internet.');

    onLine.mockReturnValue(true);
    act(() => {
      window.dispatchEvent(new Event('online'));
    });
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
