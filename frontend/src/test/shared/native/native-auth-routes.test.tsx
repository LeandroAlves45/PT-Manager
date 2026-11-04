import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useChangePasswordMutation } from '@/features/account/api/account';
import {
  useAcceptInviteMutation,
  useCompletePasswordResetMutation,
  useRegisterInvitedMutation,
  useRequestPasswordResetMutation,
} from '@/features/auth/api/auth';
import { API } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { nativeSessionResponse, resetNativeFake, storedTokens } from '@/test/native-fakes';
import { createTestQueryClient } from '@/test/render';

const fake = vi.hoisted(() => ({
  native: true,
  store: new Map<string, string>(),
  failRead: false,
  lastAccess: undefined as number | undefined,
}));

vi.mock('@/shared/native/platform', () => ({ isNativeApp: () => fake.native }));
vi.mock('@aparajita/capacitor-secure-storage', async () =>
  (await import('@/test/native-fakes')).secureStorageModule(fake)
);

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={createTestQueryClient()}>{children}</QueryClientProvider>;
}

let paths: string[];

beforeEach(() => {
  resetNativeFake(fake);
  paths = [];
  server.events.removeAllListeners();
  server.events.on('request:start', ({ request }) => {
    paths.push(new URL(request.url).pathname);
  });
});

describe('auth calls in the app', () => {
  it('creates the invited account through the native route and stores the pair', async () => {
    server.use(
      http.post(`${API}/native/auth/accept-invite/register`, () =>
        HttpResponse.json(nativeSessionResponse({ refresh_token: 'refresh-9' }))
      )
    );
    const { result } = renderHook(() => useRegisterInvitedMutation(), { wrapper });

    const session = await result.current.mutateAsync({
      token: 't',
      password: 'Segura!Password1',
      confirm_password: 'Segura!Password1',
    });

    expect(session.role).toBe('client');
    expect(paths).toEqual(['/api/v1/native/auth/accept-invite/register']);
    expect(storedTokens(fake)).toEqual({ refreshToken: 'refresh-9', csrfToken: 'csrf-1' });
  });

  it('accepts an invitation through the native route and stores the new pair', async () => {
    server.use(
      http.post(`${API}/native/auth/accept-invite`, () =>
        HttpResponse.json(nativeSessionResponse({ refresh_token: 'refresh-7' }))
      )
    );
    const { result } = renderHook(() => useAcceptInviteMutation(), { wrapper });

    await result.current.mutateAsync({ token: 't', transfer_approved: true });

    expect(paths).toEqual(['/api/v1/native/auth/accept-invite']);
    expect(storedTokens(fake)).toEqual({ refreshToken: 'refresh-7', csrfToken: 'csrf-1' });
  });

  it.each([
    [
      'asks for the reset email',
      '/api/v1/native/auth/password-reset/request',
      () => {
        const { result } = renderHook(() => useRequestPasswordResetMutation(), { wrapper });
        return () => result.current.mutateAsync({ email: 'ana@ptmanager.test' });
      },
    ],
    [
      'completes the reset',
      '/api/v1/native/auth/password-reset/complete',
      () => {
        const { result } = renderHook(() => useCompletePasswordResetMutation(), { wrapper });
        return () =>
          result.current.mutateAsync({
            token: 't',
            new_password: 'N0va!Password',
            confirm_new_password: 'N0va!Password',
          });
      },
    ],
    [
      'changes the password',
      '/api/v1/native/auth/change-password',
      () => {
        const { result } = renderHook(() => useChangePasswordMutation(), { wrapper });
        return () =>
          result.current.mutateAsync({
            current_password: 'Atual!Password1',
            new_password: 'N0va!Password',
            confirm_new_password: 'N0va!Password',
          });
      },
    ],
  ])(
    '%s through the native route (the web one needs an Origin the app has not)',
    async (_name, path, arrange) => {
      server.use(
        http.post(
          `${API}${path.replace('/api/v1', '')}`,
          () => new HttpResponse(null, { status: 204 })
        )
      );
      const call = arrange();

      await call();

      expect(paths).toEqual([path]);
    }
  );
});
