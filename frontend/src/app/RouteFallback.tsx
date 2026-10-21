/**
 * Ecrã do primeiro carregamento de uma rota lazy: o React Router não desenha nada
 * enquanto o código da página chega, e sem isto o utilizador via uma página em branco.
 */
export function RouteFallback() {
  return (
    <div
      role="status"
      aria-label="A carregar…"
      className="bg-background flex min-h-dvh items-center justify-center"
    >
      <span
        aria-hidden
        className="border-primary/30 border-t-primary size-8 animate-spin rounded-full border-4"
      />
    </div>
  );
}
