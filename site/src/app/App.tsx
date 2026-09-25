import { appLinks } from '@/config/links';
import { brandSwatches } from '@/content/brand';
import { faq } from '@/content/faq';
import { features } from '@/content/features';
import { contactEmail, primaryNav } from '@/content/navigation';
import { plans } from '@/content/plans';
import { freePlanNote } from '@/domain/plan';
import { Faq } from '@/sections/faq';
import { Features } from '@/sections/features';
import { Footer } from '@/sections/footer';
import { Header } from '@/sections/header';
import { Hero } from '@/sections/hero';
import { Pricing } from '@/sections/pricing';

/**
 * Composição da página: a única camada que junta conteúdo, configuração e secções.
 * As secções não conhecem `content/` nem `config/`; recebem tudo por props.
 */
export function App() {
  return (
    <>
      <a
        href="#conteudo"
        className="bg-primary text-primary-foreground sr-only z-[100] rounded-lg px-4 py-2.5 font-semibold focus:not-sr-only focus:fixed focus:top-2 focus:left-4"
      >
        Saltar para o conteúdo
      </a>
      <Header nav={primaryNav} loginUrl={appLinks.login} signupUrl={appLinks.signup} />
      <main id="conteudo">
        <Hero signupUrl={appLinks.signup} freePlanNote={freePlanNote(plans)} />
        <Features features={features} swatches={brandSwatches} />
        <Pricing plans={plans} signupUrl={appLinks.signup} />
        <Faq items={faq} />
      </main>
      <Footer contactEmail={contactEmail} year={__BUILD_YEAR__} />
    </>
  );
}
