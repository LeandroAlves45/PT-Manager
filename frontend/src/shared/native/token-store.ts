/**
 * Tokens da sessão nativa no armazenamento seguro do sistema (Keychain no iOS, Keystore no
 * Android).
 *
 * Na web o refresh token vive num cookie HttpOnly que o JavaScript nunca vê. Na app não há
 * cookie: o token vai e volta no corpo de `/api/v1/native/auth/*` e fica aqui, cifrado pelo
 * sistema. O plugin é importado só quando é usado, para não entrar no bundle da web.
 */

const STORAGE_KEY = 'pt-manager.native-session';

/** Par guardado: o refresh token e o segredo anti-CSRF da mesma sessão. */
export interface NativeTokens {
  refreshToken: string;
  csrfToken: string;
}

function isNativeTokens(value: unknown): value is NativeTokens {
  if (typeof value !== 'object' || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.refreshToken === 'string' &&
    candidate.refreshToken.length > 0 &&
    typeof candidate.csrfToken === 'string' &&
    candidate.csrfToken.length > 0
  );
}

/**
 * Lê os tokens guardados.
 *
 * Um valor ilegível (por exemplo, restaurado de outro telemóvel, cuja chave do Keystore não
 * veio no backup) é apagado e tratado como "sem sessão": o cliente volta a entrar.
 */
export async function readNativeTokens(): Promise<NativeTokens | null> {
  try {
    const { SecureStorage } = await import('@aparajita/capacitor-secure-storage');
    const raw = await SecureStorage.get(STORAGE_KEY, false, false);
    if (raw === null) return null;

    const parsed: unknown = typeof raw === 'string' ? JSON.parse(raw) : raw;
    if (isNativeTokens(parsed)) return parsed;
  } catch {
    // Ilegível: cai para a limpeza abaixo.
  }

  await clearNativeTokens();
  return null;
}

/**
 * Guarda os tokens da sessão. No iOS ficam só neste aparelho (`ThisDeviceOnly`): não passam
 * para outro iPhone por backup nem pelo iCloud.
 */
export async function saveNativeTokens(tokens: NativeTokens): Promise<void> {
  const { SecureStorage, KeychainAccess } = await import('@aparajita/capacitor-secure-storage');
  await SecureStorage.set(
    STORAGE_KEY,
    JSON.stringify({ refreshToken: tokens.refreshToken, csrfToken: tokens.csrfToken }),
    false,
    false,
    KeychainAccess.whenUnlockedThisDeviceOnly
  );
}

/** Apaga os tokens. Nunca lança: terminar a sessão não pode falhar por causa disto. */
export async function clearNativeTokens(): Promise<void> {
  try {
    const { SecureStorage } = await import('@aparajita/capacitor-secure-storage');
    await SecureStorage.remove(STORAGE_KEY, false);
  } catch {
    // Sem nada guardado ou plugin indisponível: não há o que apagar.
  }
}
