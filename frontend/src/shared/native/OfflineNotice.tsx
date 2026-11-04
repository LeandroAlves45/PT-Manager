import { WifiOff } from 'lucide-react';
import { useSyncExternalStore } from 'react';

function subscribe(onChange: () => void): () => void {
  window.addEventListener('online', onChange);
  window.addEventListener('offline', onChange);
  return () => {
    window.removeEventListener('online', onChange);
    window.removeEventListener('offline', onChange);
  };
}

const getOnline = (): boolean => navigator.onLine;

/**
 * Aviso fixo de "sem ligação", só na app nativa.
 *
 * Numa WebView sem rede o cliente veria pedidos a falhar sem explicação. O aviso aparece e
 * desaparece sozinho com os eventos `online`/`offline` do sistema; os pedidos voltam a ser
 * feitos pelo TanStack Query quando a ligação regressa.
 */
export function OfflineNotice() {
  const online = useSyncExternalStore(subscribe, getOnline, () => true);
  if (online) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className="bg-foreground text-background fixed inset-x-0 top-0 z-50 flex items-center justify-center gap-2 px-4 pt-[calc(env(safe-area-inset-top)+0.5rem)] pb-2 text-sm"
    >
      <WifiOff aria-hidden className="size-4" />
      Sem ligação à internet. A app volta a funcionar quando a ligação regressar.
    </div>
  );
}
