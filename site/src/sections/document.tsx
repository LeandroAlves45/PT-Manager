import { formatLongDate } from '@/domain/date';
import type { DocumentBlock, SiteDocument } from '@/domain/types';
import { Container } from '@/ui/container';

interface DocumentArticleProps {
  document: SiteDocument;
  /** Aviso mostrado enquanto o texto não tiver revisão jurídica. */
  draftNotice?: string;
}

const linkClass = 'text-primary hover:text-foreground underline underline-offset-4';

function Block({ block }: { block: DocumentBlock }) {
  switch (block.kind) {
    case 'paragraph':
      return <p className="text-muted-foreground mt-4 text-pretty">{block.text}</p>;
    case 'list':
      return (
        <ul className="text-muted-foreground mt-4 list-disc space-y-2 pl-5 text-pretty">
          {block.items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      );
    case 'links':
      return (
        <ul className="mt-4 flex flex-col gap-1">
          {block.items.map((link) => (
            <li key={link.href}>
              <a href={link.href} className={`${linkClass} inline-flex min-h-11 items-center`}>
                {link.label}
              </a>
            </li>
          ))}
        </ul>
      );
  }
}

/**
 * Página de texto (Privacidade, Termos, Suporte, Eliminar conta).
 *
 * Só HTML estático: títulos em hierarquia (um `h1`, secções em `h2`), largura de leitura
 * limitada e índice com âncoras. Não precisa de JavaScript para ser lida.
 */
export function DocumentArticle({ document, draftNotice }: DocumentArticleProps) {
  return (
    <article aria-labelledby="documento-titulo" className="py-[clamp(3rem,8vw,6rem)]">
      <Container className="max-w-[72ch]">
        <h1
          id="documento-titulo"
          className="font-display text-[clamp(2.25rem,6vw,4rem)] leading-[0.95] text-balance uppercase"
        >
          {document.title}
        </h1>
        <p className="text-muted-foreground mt-4 font-mono text-sm">
          Última atualização: {formatLongDate(document.updatedOn)}
        </p>
        {draftNotice ? (
          <p
            role="note"
            className="border-primary/40 bg-card/60 mt-6 rounded-lg border px-4 py-3 text-sm"
          >
            {draftNotice}
          </p>
        ) : null}

        {document.intro.map((text) => (
          <p key={text} className="mt-6 text-lg text-pretty">
            {text}
          </p>
        ))}

        {document.sections.length > 1 ? (
          <nav aria-label="Nesta página" className="bg-card/60 mt-8 rounded-xl border p-5">
            <p className="font-mono text-xs tracking-[0.12em] uppercase">Nesta página</p>
            <ol className="mt-3 flex flex-col gap-1">
              {document.sections.map((section) => (
                <li key={section.id}>
                  <a
                    href={`#${section.id}`}
                    className="text-muted-foreground hover:text-foreground inline-flex min-h-11 items-center text-sm"
                  >
                    {section.heading}
                  </a>
                </li>
              ))}
            </ol>
          </nav>
        ) : null}

        {document.sections.map((section) => (
          <section key={section.id} aria-labelledby={section.id} className="mt-12">
            <h2 id={section.id} className="text-2xl font-semibold text-balance">
              {section.heading}
            </h2>
            {section.blocks.map((block, index) => (
              <Block key={`${section.id}-${index}`} block={block} />
            ))}
          </section>
        ))}
      </Container>
    </article>
  );
}
