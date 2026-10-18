/**
 * Tipos mínimos da biblioteca Google Identity Services (GIS), só o que a aplicação usa.
 *
 * Declarados à mão em vez de instalar `@types/google.accounts`: a superfície é pequena
 * (`initialize` e `renderButton`) e uma dependência nova não compensa. A biblioteca em si
 * é carregada em runtime por `google-identity.ts`.
 */

/** Resposta entregue ao `callback` quando o utilizador escolhe uma conta. */
interface GoogleCredentialResponse {
  /** JWT assinado pelo Google (o `id_token` que o backend valida) */
  readonly credential: string;
  readonly select_by?: string;
}

interface GoogleIdConfiguration {
  client_id: string;
  callback: (response: GoogleCredentialResponse) => void;
  /** Valor que o Google coloca no `nonce` do id_token; tem de coincidir com o cookie do challenge. */
  nonce?: string;
  auto_select?: boolean;
}

interface GoogleButtonConfiguration {
  type?: 'standard' | 'icon';
  theme?: 'outline' | 'filled_blue' | 'filled_black';
  size?: 'large' | 'medium' | 'small';
  text?: 'signin_with' | 'signup_with' | 'continue_with' | 'signin';
  shape?: 'rectangular' | 'pill';
  logo_alignment?: 'left' | 'center';
  /** Largura em píxeis (mínimo 200, máximo 400). */
  width?: number;
  locale?: string;
}

interface GoogleAccountsId {
  initialize(configuration: GoogleIdConfiguration): void;
  renderButton(parent: HTMLElement, options: GoogleButtonConfiguration): void;
}

interface Window {
  google?: { accounts: { id: GoogleAccountsId } };
}
