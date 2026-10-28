import type { TextLink } from '@/domain/types';
import { Container } from '@/ui/container';
import { Logo } from '@/ui/logo';

interface FooterProps {
  contactEmail: string;
  year: number;
  /** Páginas de texto (Privacidade, Termos, Suporte, Eliminar conta). */
  links: readonly TextLink[];
}

const footerLinkClass =
  'text-muted-foreground hover:text-foreground inline-flex min-h-11 items-center text-sm';

/** Rodapé com contacto e as ligações para as páginas de texto. */
export function Footer({ contactEmail, year, links }: FooterProps) {
  return (
    <footer id="contacto" className="border-t">
      <Container className="flex flex-wrap justify-between gap-8 pt-12 pb-8">
        <div className="max-w-sm">
          <Logo className="h-5" />
          <p className="text-muted-foreground mt-4 text-sm">
            Plataforma de gestão para personal trainers e nutricionistas: clientes, treino,
            nutrição, suplementação, sessões e check-ins.
          </p>
        </div>
        <nav aria-label="Rodapé">
          <ul className="flex flex-wrap gap-x-5 gap-y-1">
            {links.map((link) => (
              <li key={link.href}>
                <a href={link.href} className={footerLinkClass}>
                  {link.label}
                </a>
              </li>
            ))}
            <li>
              <a href={`mailto:${contactEmail}`} className={footerLinkClass}>
                Contacto: {contactEmail}
              </a>
            </li>
          </ul>
        </nav>
      </Container>
      <Container className="text-muted-foreground border-t pt-5 pb-8 font-mono text-sm">
        © {year} PT Manager. Todos os direitos reservados.
      </Container>
    </footer>
  );
}
