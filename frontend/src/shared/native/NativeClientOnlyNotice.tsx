import { useAuth } from '@/app/providers/useAuth';
import { Button } from '@/shared/components/ui/button';

/** Endereço da área web de personal trainers e superuser. */
const WEB_APP_URL = 'https://app.ptmanager.pt';

/**
 * Ecrã da app nativa para quem não é cliente.
 *
 * A app v1 é só o portal do cliente. A área do personal trainer tem a subscrição paga, e as
 * lojas exigiriam a compra pela loja: fica na web. Sem link para a web aqui dentro, para não
 * contar como encaminhar para um pagamento fora da loja.
 */
export function NativeClientOnlyNotice() {
  const { signOut } = useAuth();

  return (
    <main className="bg-background flex min-h-dvh flex-col items-center justify-center gap-4 px-6 pt-[env(safe-area-inset-top)] text-center">
      <h1 className="font-display text-2xl">Esta app é para clientes</h1>
      <p className="text-muted-foreground max-w-sm text-sm">
        A área de personal trainer está disponível no computador ou no browser do telemóvel, em{' '}
        <span className="text-foreground font-medium">{WEB_APP_URL.replace('https://', '')}</span>.
      </p>
      <Button className="min-h-11" onClick={() => void signOut()}>
        Terminar sessão
      </Button>
    </main>
  );
}
