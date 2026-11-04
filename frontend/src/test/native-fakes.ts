import type { components } from '@/shared/api/schema';

type NativeSessionResponse = components['schemas']['NativeSessionResponse'];

/** Estado partilhado pelos mocks da plataforma e do armazenamento seguro (ver `vi.hoisted`). */
export interface NativeFakeState {
  native: boolean;
  store: Map<string, string>;
  failRead: boolean;
  lastAccess: number | undefined;
}

export const STORAGE_KEY = 'pt-manager.native-session';

/** Estado inicial: dentro da app, sem nada guardado. */
export function createNativeFakeState(): NativeFakeState {
  return { native: true, store: new Map(), failRead: false, lastAccess: undefined };
}

/** Volta ao estado inicial entre testes. */
export function resetNativeFake(state: NativeFakeState): void {
  state.native = true;
  state.store.clear();
  state.failRead = false;
  state.lastAccess = undefined;
}

/**
 * Implementação falsa de `@aparajita/capacitor-secure-storage` sobre um `Map`. Ler pode
 * falhar à vontade (`failRead`), como um Keystore cuja chave não veio num backup.
 */
export function secureStorageModule(state: NativeFakeState) {
  return {
    KeychainAccess: { whenUnlocked: 0, whenUnlockedThisDeviceOnly: 1 },
    SecureStorage: {
      get: (key: string) =>
        state.failRead
          ? Promise.reject(new Error('keystore key lost'))
          : Promise.resolve(state.store.get(key) ?? null),
      set: (key: string, value: string, _date: boolean, _sync: boolean, access?: number) => {
        state.store.set(key, value);
        state.lastAccess = access;
        return Promise.resolve();
      },
      remove: (key: string) => Promise.resolve(state.store.delete(key)),
    },
  };
}

/** Resposta de `/api/v1/native/auth/*` com o refresh token no corpo. */
export function nativeSessionResponse(
  overrides: Partial<NativeSessionResponse> = {}
): NativeSessionResponse {
  return {
    user_id: '11111111-1111-1111-1111-111111111111',
    trainer_id: '22222222-2222-2222-2222-222222222222',
    role: 'client',
    access_token: 'access-1',
    access_token_expires_at: '2026-11-04T12:00:00Z',
    refresh_token: 'refresh-1',
    refresh_token_expires_at: '2026-12-04T12:00:00Z',
    csrf_token: 'csrf-1',
    ...overrides,
  };
}

/** Guarda um par no armazenamento falso, como depois de um login na app. */
export function storeTokens(state: NativeFakeState, refreshToken: string, csrfToken: string): void {
  state.store.set(STORAGE_KEY, JSON.stringify({ refreshToken, csrfToken }));
}

/** Par guardado, ou `null`. */
export function storedTokens(state: NativeFakeState): unknown {
  const raw = state.store.get(STORAGE_KEY);
  return raw === undefined ? null : (JSON.parse(raw) as unknown);
}
