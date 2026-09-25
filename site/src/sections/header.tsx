import { useEffect, useId, useState } from 'react';
import { Menu, X } from 'lucide-react';

import type { NavLink } from '@/domain/types';
import { ButtonLink } from '@/ui/button-link';
import { Container } from '@/ui/container';
import { Logo } from '@/ui/logo';

interface HeaderProps {
  nav: readonly NavLink[];
  loginUrl: string;
  signupUrl: string;
}

/**
 * Cabeçalho fixo com navegação por âncoras.
 *
 * O desktop e o mobile trocam por media query (CSS), não por `window.innerWidth`: o HTML
 * pré-renderizado já sai certo em qualquer largura e não há salto na hidratação.
 */
export function Header({ nav, loginUrl, signupUrl }: HeaderProps) {
  const [open, setOpen] = useState(false);
  const menuId = useId();

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open]);

  const close = () => setOpen(false);

  return (
    <header className="bg-background/70 sticky top-0 z-50 border-b backdrop-blur-md backdrop-saturate-150">
      <Container>
        <nav aria-label="Principal" className="flex h-16 items-center gap-6">
          <a href="#topo" aria-label="PT Manager — início" className="flex min-h-11 items-center">
            <Logo />
          </a>

          <ul className="ml-4 hidden gap-1 md:flex">
            {nav.map((link) => (
              <li key={link.section}>
                <a
                  href={`#${link.section}`}
                  className="text-muted-foreground hover:bg-foreground/5 hover:text-foreground inline-flex min-h-11 items-center rounded-lg px-3 text-sm transition-colors"
                >
                  {link.label}
                </a>
              </li>
            ))}
          </ul>

          <div className="ml-auto flex items-center gap-2">
            <ButtonLink href={loginUrl} variant="ghost" className="hidden md:inline-flex">
              Entrar
            </ButtonLink>
            <ButtonLink href={signupUrl}>Criar conta</ButtonLink>
            <button
              type="button"
              onClick={() => setOpen((value) => !value)}
              aria-expanded={open}
              aria-controls={menuId}
              aria-label={open ? 'Fechar menu' : 'Abrir menu'}
              className="text-foreground inline-flex size-11 items-center justify-center rounded-lg border md:hidden"
            >
              {open ? (
                <X aria-hidden="true" className="size-5" />
              ) : (
                <Menu aria-hidden="true" className="size-5" />
              )}
            </button>
          </div>
        </nav>
      </Container>

      <div id={menuId} hidden={!open} className="border-t md:hidden">
        <Container>
          <ul className="flex flex-col py-2">
            {nav.map((link) => (
              <li key={link.section} className="border-b last:border-b-0">
                <a
                  href={`#${link.section}`}
                  onClick={close}
                  className="flex min-h-11 items-center py-3"
                >
                  {link.label}
                </a>
              </li>
            ))}
            <li>
              <a href={loginUrl} onClick={close} className="flex min-h-11 items-center py-3">
                Entrar
              </a>
            </li>
          </ul>
        </Container>
      </div>
    </header>
  );
}
