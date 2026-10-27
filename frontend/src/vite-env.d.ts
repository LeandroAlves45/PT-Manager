/// <reference types="vite/client" />

/** Variáveis de ambiente do Vite usadas pela aplicação. */
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
  readonly VITE_GOOGLE_CLIENT_ID?: string;
  /** DSN do projeto Sentry do frontend; sem ele a monitorização fica desligada. */
  readonly VITE_SENTRY_DSN?: string;
  /** Identificador da versão (commit), para agrupar erros por deploy. */
  readonly VITE_SENTRY_RELEASE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
