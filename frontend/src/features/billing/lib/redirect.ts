/**
 * Sai da app para uma página alojada do Stripe (Checkout ou portal).
 *
 * Isolado num módulo para os testes o substituírem: o jsdom não navega e
 * `window.location.assign` não é redefinível.
 *
 * Só aceita `https:` — o servidor já o garante (`StripeCheckoutGateway`), mas um URL de
 * outro esquema nunca deve chegar ao `location`.
 */
export function redirectToProvider(url: string): void {
  if (new URL(url).protocol !== 'https:') throw new Error('URL de pagamento inválido.');
  window.location.assign(url);
}
