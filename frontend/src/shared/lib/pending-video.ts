/**
 * Vídeo escolhido em «Criar exercício», à espera de o exercício existir.
 *
 * O envio precisa do id do exercício, que só existe depois de o criar. A página de criação
 * deixa aqui o ficheiro e abre a edição; a edição lê-o e começa o envio. Fica só em memória,
 * de uso único e preso ao exercício certo: nunca vai para o histórico, storage ou URL.
 */

let pending: { exerciseId: string; file: File } | null = null;

/** Guarda o ficheiro para o exercício acabado de criar. */
export function setPendingExerciseVideo(exerciseId: string, file: File): void {
  pending = { exerciseId, file };
}

/** Lê sem consumir: seguro num inicializador que o StrictMode repete. */
export function peekPendingExerciseVideo(exerciseId: string): File | null {
  return pending?.exerciseId === exerciseId ? pending.file : null;
}

/** Esquece o ficheiro depois de o envio começar, para não se repetir ao voltar à página. */
export function consumePendingExerciseVideo(exerciseId: string): void {
  if (pending?.exerciseId === exerciseId) pending = null;
}

/** Só para testes: o estado vive no módulo. */
export function resetPendingExerciseVideo(): void {
  pending = null;
}
