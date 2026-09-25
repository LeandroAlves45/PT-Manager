/**
 * Modelo do conteúdo do site.
 *
 * Tipos puros, sem React nem dependências: as secções recebem estes dados por props e o
 * conteúdo concreto vive em `content/`. Mudar um texto nunca exige tocar num componente.
 */

/** Identificadores estáveis das âncoras da página (também usados na navegação). */
export type SectionId = 'topo' | 'conteudo' | 'funcionalidades' | 'planos' | 'faq' | 'contacto';

export interface NavLink {
  label: string;
  section: SectionId;
}

/** Ícones disponíveis para funcionalidades; o mapeamento para SVG fica na apresentação. */
export type FeatureIcon =
  'nutrition' | 'training' | 'clients' | 'supplements' | 'checkins' | 'assessments' | 'sessions';

/** Largura de um cartão na grelha bento (em colunas de 6, no desktop). */
export type FeatureSpan = 'third' | 'half';

export interface Feature {
  id: string;
  icon: FeatureIcon;
  title: string;
  description: string;
  span: FeatureSpan;
  tags?: readonly { label: string; highlight?: boolean }[];
}

/** Espelha `SubscriptionTier` do backend (FREE, STARTER, PRO). */
export type PlanCode = 'FREE' | 'STARTER' | 'PRO';

export interface Plan {
  code: PlanCode;
  /** Preço mensal em euros (inteiro). */
  monthlyPriceEur: number;
  /** `null` = ilimitado, como `ClientLimit` no backend. */
  clientLimit: number | null;
  priceNote: string;
  benefits: readonly string[];
  highlighted: boolean;
  badge?: string;
}

export interface FaqItem {
  id: string;
  question: string;
  answer: string;
}

export interface BrandSwatch {
  id: 'blue' | 'orange' | 'green' | 'pink' | 'violet';
  label: string;
  /** Valor CSS aplicado à pré-visualização via CSSOM (nunca via atributo `style`). */
  value: string;
}
