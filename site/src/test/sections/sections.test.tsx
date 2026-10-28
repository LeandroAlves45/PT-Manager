import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { brandSwatches } from '@/content/brand';
import { faq } from '@/content/faq';
import { features } from '@/content/features';
import { primaryNav } from '@/content/navigation';
import { plans } from '@/content/plans';
import { BrandShowcase } from '@/sections/brand-showcase';
import { Faq } from '@/sections/faq';
import { Features } from '@/sections/features';
import { Footer } from '@/sections/footer';
import { DocumentArticle } from '@/sections/document';
import { Header } from '@/sections/header';
import type { SiteDocument } from '@/domain/types';
import { Pricing } from '@/sections/pricing';

const LOGIN = 'https://app.example.pt/entrar';
const SIGNUP = 'https://app.example.pt/criar-conta';

describe('Header', () => {
  it('liga "Entrar" e "Criar conta" à app e a navegação às âncoras', () => {
    render(<Header nav={primaryNav} loginUrl={LOGIN} signupUrl={SIGNUP} />);
    const nav = screen.getByRole('navigation', { name: 'Principal' });
    expect(within(nav).getByRole('link', { name: 'Criar conta' })).toHaveAttribute('href', SIGNUP);
    expect(within(nav).getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', LOGIN);
    expect(within(nav).getByRole('link', { name: 'Planos' })).toHaveAttribute('href', '#planos');
  });

  it('abre o menu mobile, fecha com Escape e com clique num link', async () => {
    const user = userEvent.setup();
    render(<Header nav={primaryNav} loginUrl={LOGIN} signupUrl={SIGNUP} />);
    const toggle = screen.getByRole('button', { name: 'Abrir menu' });
    const menu = document.getElementById(toggle.getAttribute('aria-controls')!)!;

    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(menu).not.toBeVisible();

    await user.click(toggle);
    expect(screen.getByRole('button', { name: 'Fechar menu' })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
    expect(menu).toBeVisible();

    await user.keyboard('{Escape}');
    expect(menu).not.toBeVisible();

    await user.click(screen.getByRole('button', { name: 'Abrir menu' }));
    await user.click(within(menu).getByRole('link', { name: 'FAQ' }));
    expect(menu).not.toBeVisible();
  });

  it('nenhum link abre noutro separador (sem target=_blank)', () => {
    const { container } = render(<Header nav={primaryNav} loginUrl={LOGIN} signupUrl={SIGNUP} />);
    expect(container.querySelector('[target]')).toBeNull();
  });
});

describe('Features / BrandShowcase', () => {
  it('mostra um cartão por funcionalidade com título h3', () => {
    render(<Features features={features} swatches={brandSwatches} />);
    for (const feature of features) {
      expect(screen.getByRole('heading', { level: 3, name: feature.title })).toBeInTheDocument();
    }
  });

  it('o seletor de cor marca a amostra escolhida e aplica a cor via CSSOM', async () => {
    const user = userEvent.setup();
    const { container } = render(<BrandShowcase swatches={brandSwatches} />);
    const blue = screen.getByRole('button', { name: 'Cor de exemplo: Azul' });
    const orange = screen.getByRole('button', { name: 'Cor de exemplo: Laranja' });

    expect(blue).toHaveAttribute('aria-pressed', 'true');
    expect(orange).toHaveAttribute('aria-pressed', 'false');

    await user.click(orange);

    expect(orange).toHaveAttribute('aria-pressed', 'true');
    expect(blue).toHaveAttribute('aria-pressed', 'false');
    const preview = container.querySelector<HTMLElement>('[class*="grid"]')!;
    expect(preview.style.getPropertyValue('--demo-brand')).toBe('#fb923c');
  });
});

describe('Pricing', () => {
  it('mostra os três planos com preço, limite e CTA para o signup', () => {
    render(<Pricing plans={plans} signupUrl={SIGNUP} />);
    for (const plan of plans) {
      const card = screen.getByRole('article', { name: plan.code });
      expect(within(card).getByText(`${plan.monthlyPriceEur}€`)).toBeInTheDocument();
      expect(
        within(card).getByText(
          plan.clientLimit === null ? 'Clientes ilimitados' : `Até ${plan.clientLimit} clientes`
        )
      ).toBeInTheDocument();
      expect(within(card).getByRole('link', { name: /Criar conta grátis/ })).toHaveAttribute(
        'href',
        SIGNUP
      );
    }
    expect(screen.getByText('Mais popular')).toBeInTheDocument();
  });
});

describe('Faq', () => {
  // O jsdom não implementa a ativação de <summary> por Enter/Espaço (comportamento nativo do
  // browser); o teclado é verificado no browser real. Aqui prova-se a estrutura e o toggle.
  it('é um acordeão nativo: primeira aberta, as outras abrem ao ativar o summary', async () => {
    const user = userEvent.setup();
    const { container } = render(<Faq items={faq} />);
    const details = [...container.querySelectorAll('details')];

    expect(details).toHaveLength(faq.length);
    expect(details[0]).toHaveAttribute('open');
    expect(details[1]).not.toHaveAttribute('open');
    expect(details.every((d) => d.getAttribute('name') === 'faq')).toBe(true);

    await user.click(details[1]!.querySelector('summary')!);
    expect(details[1]).toHaveAttribute('open');
  });
});

describe('Footer', () => {
  const links = [
    { label: 'Privacidade', href: '/privacidade' },
    { label: 'Termos', href: '/termos' },
  ];

  it('tem contacto por email e as ligações para as páginas de texto', () => {
    render(<Footer contactEmail="contacto@ptmanager.pt" year={2026} links={links} />);
    const footer = screen.getByRole('navigation', { name: 'Rodapé' });
    expect(within(footer).getByRole('link', { name: /contacto@ptmanager.pt/ })).toHaveAttribute(
      'href',
      'mailto:contacto@ptmanager.pt'
    );
    expect(within(footer).getByRole('link', { name: 'Privacidade' })).toHaveAttribute(
      'href',
      '/privacidade'
    );
    expect(within(footer).getByRole('link', { name: 'Termos' })).toHaveAttribute('href', '/termos');
    expect(screen.getByText(/© 2026 PT Manager/)).toBeInTheDocument();
  });
});

describe('Header fora da página inicial', () => {
  it('as âncoras e o logótipo voltam à página inicial', () => {
    render(<Header nav={primaryNav} loginUrl={LOGIN} signupUrl={SIGNUP} onHomePage={false} />);
    const nav = screen.getByRole('navigation', { name: 'Principal' });
    expect(within(nav).getByRole('link', { name: 'Planos' })).toHaveAttribute('href', '/#planos');
    expect(within(nav).getByRole('link', { name: 'PT Manager, página inicial' })).toHaveAttribute(
      'href',
      '/'
    );
  });
});

describe('DocumentArticle', () => {
  const document: SiteDocument = {
    path: '/termos',
    linkLabel: 'Termos',
    title: 'Termos de utilização',
    description: 'Descrição',
    updatedOn: '2026-10-28',
    intro: ['Introdução.'],
    sections: [
      { id: 'a', heading: 'Primeira', blocks: [{ kind: 'paragraph', text: 'Texto A.' }] },
      {
        id: 'b',
        heading: 'Segunda',
        blocks: [
          { kind: 'list', items: ['Item 1', 'Item 2'] },
          { kind: 'links', items: [{ label: 'Suporte', href: '/suporte' }] },
        ],
      },
    ],
  };

  it('tem um h1, um h2 por secção, índice com âncoras e a data por extenso', () => {
    render(<DocumentArticle document={document} />);
    expect(screen.getByRole('heading', { level: 1, name: 'Termos de utilização' })).toBeVisible();
    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual([
      'Primeira',
      'Segunda',
    ]);
    const toc = screen.getByRole('navigation', { name: 'Nesta página' });
    expect(within(toc).getByRole('link', { name: 'Segunda' })).toHaveAttribute('href', '#b');
    expect(screen.getByText(/28 de outubro de 2026/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Suporte' })).toHaveAttribute('href', '/suporte');
    expect(screen.getByText('Item 2')).toBeInTheDocument();
  });

  it('mostra o aviso de rascunho só quando existe', () => {
    const { rerender } = render(<DocumentArticle document={document} />);
    expect(screen.queryByRole('note')).toBeNull();
    rerender(<DocumentArticle document={document} draftNotice="Versão provisória." />);
    expect(screen.getByRole('note')).toHaveTextContent('Versão provisória.');
  });
});
