import { describe, expect, it } from 'vitest';

import { redirectToProvider } from '@/features/billing/lib/redirect';

describe('redirectToProvider', () => {
  it.each(['http://checkout.stripe.com/c/pay/x', 'javascript:alert(1)'])(
    'refuses %s before touching the location',
    (url) => {
      expect(() => redirectToProvider(url)).toThrow('URL de pagamento inválido.');
    }
  );
});
