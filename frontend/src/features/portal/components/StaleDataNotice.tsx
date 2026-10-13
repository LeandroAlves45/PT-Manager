import { Button } from '@/shared/components/ui/button';

/**
 * Aviso de que a última atualização falhou e o ecrã mostra os dados que já tinha.
 *
 * Substitui o ecrã de erro quando há dados em cache: uma falha de rede a meio não pode
 * esconder o treino ou as tomas que o cliente já estava a ver.
 *
 * @param retrying Verdadeiro enquanto a nova tentativa corre; desativa o botão.
 */
export function StaleDataNotice({ onRetry, retrying }: { onRetry: () => void; retrying: boolean }) {
  return (
    <div
      role="status"
      className="border-border bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm"
    >
      <p>Não foi possível atualizar. Estás a ver os últimos dados carregados.</p>
      <Button
        type="button"
        variant="outline"
        className="min-h-11"
        disabled={retrying}
        onClick={onRetry}
      >
        Tentar novamente
      </Button>
    </div>
  );
}
