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
import { Header } from '@/sections/header';
import { Pricing } from '@/sections/pricing';

const LOGIN = 'https://app.example.pt/auth/login';
const SIGNUP = 'https://app.example.pt/auth/signup';

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
  it('tem contacto por email e nenhum link morto', () => {
    const { container } = render(<Footer contactEmail="contacto@ptmanager.pt" year={2026} />);
    expect(screen.getByRole('link', { name: /contacto@ptmanager.pt/ })).toHaveAttribute(
      'href',
      'mailto:contacto@ptmanager.pt'
    );
    expect(container.querySelector('a[href="#privacidade"], a[href="#termos"]')).toBeNull();
    expect(screen.getByText(/© 2026 PT Manager/)).toBeInTheDocument();
  });
});
