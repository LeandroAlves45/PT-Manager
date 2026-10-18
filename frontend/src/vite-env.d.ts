/// <reference types="vite/client" />

/** Variáveis de ambiente do Vite usadas pela aplicação. */
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
  readonly VITE_GOOGLE_CLIENT_ID?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
