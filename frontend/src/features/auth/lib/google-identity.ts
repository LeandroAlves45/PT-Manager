/**
 * Carregador do script Google Identity Services.
 *
 * O script é um recurso externo e opcional: só é pedido quando alguém monta um botão Google
 * (nunca no arranque da aplicação) e uma única vez por separador, mesmo com StrictMode ou
 * vários botões na mesma página. Falhas e timeouts rejeitam a promessa e libertam o
 * singleton, para uma nova tentativa poder voltar a pedir o script.
 */

const GSI_URL = 'https://accounts.google.com/gsi/client';
const LOAD_TIMEOUT_MS = 10_000;

let loading: Promise<GoogleAccountsId> | null = null;

function readLoaded(): GoogleAccountsId | null {
  return window.google?.accounts.id ?? null;
}

/**
 * Carrega o GIS e devolve `google.accounts.id`.
 *
 * @param timeoutMs Tempo máximo à espera do script; por omissão 10 s.
 * @throws Error quando o script falha a carregar, demora demasiado ou não expõe a API.
 */
export function loadGoogleIdentity(timeoutMs: number = LOAD_TIMEOUT_MS): Promise<GoogleAccountsId> {
  const ready = readLoaded();
  if (ready !== null) return Promise.resolve(ready);

  loading ??= new Promise<GoogleAccountsId>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = GSI_URL;
    script.async = true;
    script.defer = true;

    const timer = window.setTimeout(
      () => fail(new Error('Timeout a carregar o Google.')),
      timeoutMs
    );

    function fail(error: Error): void {
      window.clearTimeout(timer);
      script.remove();
      loading = null;
      reject(error);
    }

    script.addEventListener('load', () => {
      window.clearTimeout(timer);
      const api = readLoaded();
      if (api === null) fail(new Error('O script do Google não expôs a API.'));
      else resolve(api);
    });
    script.addEventListener('error', () => fail(new Error('Não foi possível carregar o Google.')));

    document.head.appendChild(script);
  });

  return loading;
}

/** Esquece o carregamento em curso e remove o script. Só para testes. */
export function resetGoogleIdentityLoader(): void {
  loading = null;
  for (const script of document.querySelectorAll(`script[src="${GSI_URL}"]`)) script.remove();
}
