import { draftNotice, footerLinks, siteDocuments } from '@/app/pages';
import { appLinks } from '@/config/links';
import { brandSwatches } from '@/content/brand';
import { faq } from '@/content/faq';
import { features } from '@/content/features';
import { findSiteDocument } from '@/content/legal';
import { contactEmail, primaryNav } from '@/content/navigation';
import { plans } from '@/content/plans';
import type { PagePath } from '@/domain/types';
import { freePlanNote } from '@/domain/plan';
import { DocumentArticle } from '@/sections/document';
import { Faq } from '@/sections/faq';
import { Features } from '@/sections/features';
import { Footer } from '@/sections/footer';
import { Header } from '@/sections/header';
import { Hero } from '@/sections/hero';
import { Pricing } from '@/sections/pricing';

interface AppProps {
  /** Rota a mostrar. O pré-render e o cliente passam a mesma, para a hidratação bater certo. */
  path?: PagePath;
}

/**
 * Composição das páginas: a única camada que junta conteúdo, configuração e secções.
 * As secções não conhecem `content/` nem `config/`; recebem tudo por props.
 */
export function App({ path = '/' }: AppProps) {
  const document = path === '/' ? undefined : findSiteDocument(siteDocuments, path);

  return (
    <>
      <a
        href="#conteudo"
        className="bg-primary text-primary-foreground sr-only z-[100] rounded-lg px-4 py-2.5 font-semibold focus:not-sr-only focus:fixed focus:top-2 focus:left-4"
      >
        Saltar para o conteúdo
      </a>
      <Header
        nav={primaryNav}
        loginUrl={appLinks.login}
        signupUrl={appLinks.signup}
        onHomePage={!document}
      />
      <main id="conteudo">
        {document ? (
          <DocumentArticle document={document} draftNotice={draftNotice} />
        ) : (
          <>
            <Hero signupUrl={appLinks.signup} freePlanNote={freePlanNote(plans)} />
            <Features features={features} swatches={brandSwatches} />
            <Pricing plans={plans} signupUrl={appLinks.signup} />
            <Faq items={faq} />
          </>
        )}
      </main>
      <Footer contactEmail={contactEmail} year={__BUILD_YEAR__} links={footerLinks} />
    </>
  );
}
