import { describe, expect, it } from 'vitest';

import { buildAppLinks } from '@/config/links';
import { DEFAULT_APP_URL, DEFAULT_SITE_URL, parseOrigin, readSiteConfig } from '@/config/env';

describe('parseOrigin', () => {
  it('normaliza uma origem https (remove a barra final)', () => {
    expect(parseOrigin('X', 'https://ptmanager.pt/', '')).toBe('https://ptmanager.pt');
  });

  it('usa o valor por omissão quando a variável está vazia', () => {
    expect(parseOrigin('X', '  ', DEFAULT_SITE_URL)).toBe(DEFAULT_SITE_URL);
    expect(parseOrigin('X', undefined, DEFAULT_APP_URL)).toBe(DEFAULT_APP_URL);
  });

  it('aceita http só em localhost', () => {
    expect(parseOrigin('X', 'http://localhost:5173', '')).toBe('http://localhost:5173');
    expect(() => parseOrigin('X', 'http://ptmanager.pt', '')).toThrow(/https/);
  });

  it.each([
    ['javascript:alert(1)', /https/],
    ['ftp://ptmanager.pt', /https/],
    ['não é url', /URL válido/],
    ['https://user:pass@ptmanager.pt', /credenciais/],
    ['https://ptmanager.pt/app', /só a origem/],
    ['https://ptmanager.pt/?next=//evil.example', /só a origem/],
    ['https://ptmanager.pt/#x', /só a origem/],
  ])('recusa %s', (value, error) => {
    expect(() => parseOrigin('VITE_APP_URL', value, '')).toThrow(error);
  });

  it('inclui o nome da variável na mensagem de erro', () => {
    expect(() => parseOrigin('VITE_APP_URL', 'http://x.pt', '')).toThrow(/VITE_APP_URL/);
  });
});

describe('readSiteConfig / buildAppLinks', () => {
  it('produz os links de produção por omissão', () => {
    const config = readSiteConfig({});
    expect(config).toEqual({ siteUrl: 'https://ptmanager.pt', appUrl: 'https://app.ptmanager.pt' });
    expect(buildAppLinks(config)).toEqual({
      login: 'https://app.ptmanager.pt/auth/login',
      signup: 'https://app.ptmanager.pt/auth/login',
    });
  });

  it('respeita as variáveis definidas', () => {
    const config = readSiteConfig({
      VITE_SITE_URL: 'https://staging.ptmanager.pt',
      VITE_APP_URL: 'https://app.staging.ptmanager.pt',
    });
    expect(buildAppLinks(config).login).toBe('https://app.staging.ptmanager.pt/auth/login');
  });
});
