/**
 * "Flash" de aviso para o próximo ecrã de login, de uso único e só em memória.
 *
 * Existe porque há avisos que nascem num ecrã protegido cuja sessão acaba (mudar a
 * palavra-passe): quem navega para o login é o `RequireAuth`, que leva `state.from` mas não
 * sabe de avisos. O ecrã que termina a sessão deixa aqui o aviso; o login lê-o ao montar e
 * limpa-o. Nada vai para storage nem para o URL.
 */

let pending: string | null = null;

/** Deixa o aviso para o próximo login. */
export function setPendingAuthNotice(message: string): void {
  pending = message;
}

/** Lê o aviso sem o consumir: seguro chamar num inicializador que o StrictMode repete. */
export function peekPendingAuthNotice(): string | null {
  return pending;
}

/** Devolve o aviso e esquece-o, para não reaparecer num segundo login. */
export function consumePendingAuthNotice(): string | null {
  const message = pending;
  pending = null;
  return message;
}

/** Esquece o aviso pendente. Só para testes: o estado vive no módulo. */
export function resetPendingAuthNotice(): void {
  pending = null;
}
