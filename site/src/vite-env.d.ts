/// <reference types="vite/client" />

/** Ano do build, injetado por `define` no vite.config (igual no SSR e no cliente). */
declare const __BUILD_YEAR__: number;

interface ImportMetaEnv {
  readonly VITE_SITE_URL?: string;
  readonly VITE_APP_URL?: string;
}
