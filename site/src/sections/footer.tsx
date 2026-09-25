import { Container } from '@/ui/container';
import { Logo } from '@/ui/logo';

interface FooterProps {
  contactEmail: string;
  year: number;
}

/**
 * Rodapé. Os links de Privacidade e Termos voltam quando existirem páginas reais: um link
 * para uma página inexistente é pior do que nenhum.
 */
export function Footer({ contactEmail, year }: FooterProps) {
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
            <li>
              <a
                href={`mailto:${contactEmail}`}
                className="text-muted-foreground hover:text-foreground inline-flex min-h-11 items-center text-sm"
              >
                Contacto — {contactEmail}
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
