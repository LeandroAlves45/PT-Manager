import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';

import { useAuth } from '@/app/providers/useAuth';
import { useClientSearchQuery } from '@/features/clients';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/shared/components/ui/command';
import { navigationFor } from '@/shared/config/navigation';
import { useDebounce } from '@/shared/hooks/useDebounce';

/**
 * Paleta de comandos (⌘K / Ctrl K).
 *
 * Decisões do desenho:
 * - sem atalhos globais além de ⌘K: `⌘⇧S` e companhia colidem com o browser;
 * - grupos de Navegação e de Clientes; a pesquisa transversal é futura,
 *   por isso a 6C pesquisa por recurso, e só o trainer tem `GET /clients`;
 * - pesquisa a partir de dois caracteres, com 300 ms de espera.
 *
 * Vive em `app/` e não em `shared/` porque consome uma feature, e `shared` nunca importa
 * de `features`.
 */
export function CommandMenu() {
  const [open, setOpen] = useState(false);
  const [term, setTerm] = useState('');
  const navigate = useNavigate();
  const { session } = useAuth();
  const debouncedTerm = useDebounce(term, 300);

  const isTrainer = session?.role === 'trainer';
  // Com o termo vazio não se pesquisa, mesmo que o valor com debounce ainda traga o termo
  // anterior: sem isto, reabrir o menu mostrava durante 300 ms os resultados da última vez.
  const searchTerm = open && isTrainer && term.trim() !== '' ? debouncedTerm : '';
  const { data: clients } = useClientSearchQuery(searchTerm);

  function handleOpenChange(nextOpen: boolean): void {
    setOpen(nextOpen);
    if (!nextOpen) {
      setTerm('');
    }
  }

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setOpen((previous) => !previous);
        // Abrir ou fechar pelo atalho começa sempre sem termo, como fechar pelo diálogo.
        setTerm('');
      }
    }

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  if (session === null) return null;

  function go(route: string): void {
    setOpen(false);
    setTerm('');
    void navigate(route);
  }

  return (
    <CommandDialog
      open={open}
      onOpenChange={handleOpenChange}
      title="Pesquisar"
      description="Navegar e procurar"
    >
      <CommandInput placeholder="Pesquisar…" value={term} onValueChange={setTerm} />
      <CommandList>
        <CommandEmpty>Sem resultados.</CommandEmpty>

        {navigationFor(session.role).map((group) => (
          <CommandGroup key={group.label} heading={group.label}>
            {group.items.map((item) => (
              <CommandItem key={item.route} value={item.label} onSelect={() => go(item.route)}>
                <item.icon aria-hidden />
                {item.label}
              </CommandItem>
            ))}
          </CommandGroup>
        ))}

        {clients !== undefined && clients.items.length > 0 && (
          <CommandGroup heading="Clientes">
            {clients.items.map((client) => (
              <CommandItem
                key={client.id}
                value={client.name}
                onSelect={() => go(`/trainer/clients/${client.id}`)}
              >
                {client.name}
              </CommandItem>
            ))}
          </CommandGroup>
        )}
      </CommandList>
    </CommandDialog>
  );
}
