import { describe, expect, it } from 'vitest';

import {
  adjustToContrast,
  contrastRatio,
  isHexColor,
  pickForeground,
  relativeLuminance,
} from '@/shared/lib/contrast';

describe('contrast', () => {
  it('accepts only #RRGGBB colours', () => {
    expect(isHexColor('#E8642A')).toBe(true);
    expect(isHexColor('#e8642a')).toBe(true);
    expect(isHexColor('E8642A')).toBe(false);
    expect(isHexColor('#E8642')).toBe(false);
    expect(isHexColor('#E8642A; color: red')).toBe(false);
    expect(isHexColor('red')).toBe(false);
  });

  it('computes the WCAG relative luminance at the extremes and below the 0.04045 threshold', () => {
    expect(relativeLuminance('#000000')).toBe(0);
    expect(relativeLuminance('#ffffff')).toBeCloseTo(1, 10);
    // 10/255 ≈ 0,0392 fica abaixo do limiar: ramo linear (c / 12,92), só no verde.
    expect(relativeLuminance('#000a00')).toBeCloseTo(0.7152 * (10 / 255 / 12.92), 10);
  });

  it('matches the reference contrast ratios of the design system', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrastRatio('#ffffff', '#ffffff')).toBe(1);
    // Valores do doc 03 §3: branco sobre o azul claro falha AA, o azul escuro passa.
    expect(contrastRatio('#ffffff', '#00a3e9')).toBeCloseTo(2.83, 1);
    expect(contrastRatio('#03131c', '#00a3e9')).toBeCloseTo(6.66, 1);
    expect(contrastRatio('#0077b6', '#ffffff')).toBeCloseTo(4.87, 1);
    // A ordem dos argumentos não muda o rácio.
    expect(contrastRatio('#00a3e9', '#03131c')).toBe(contrastRatio('#03131c', '#00a3e9'));
  });

  it('picks black or white, whichever contrasts more', () => {
    expect(pickForeground('#112233')).toBe('#ffffff');
    expect(pickForeground('#ffe600')).toBe('#000000');
    expect(pickForeground('#e8642a')).toBe('#000000');
    // O cinzento médio favorece o preto (5,0 contra 4,2): o branco ficaria abaixo de AA.
    expect(pickForeground('#777777')).toBe('#000000');
  });

  it('returns null when not even black or white reaches the minimum', () => {
    expect(pickForeground('#777777', 7)).toBeNull();
  });

  it('keeps a colour that already passes against every background', () => {
    expect(adjustToContrast('#1E3A8A', ['#f7f8fa', '#ffffff'])).toBe('#1e3a8a');
  });

  it('darkens a light brand colour on light backgrounds until it passes AA', () => {
    const adjusted = adjustToContrast('#E8642A', ['#f7f8fa', '#ffffff']);

    expect(adjusted).toBe('#ba5022');
    expect(contrastRatio('#ba5022', '#f7f8fa')).toBeGreaterThanOrEqual(4.5);
    expect(contrastRatio('#ba5022', '#ffffff')).toBeGreaterThanOrEqual(4.5);
    // É o primeiro passo que passa: o anterior (15 % de preto) ainda falha.
    expect(contrastRatio('#c55524', '#f7f8fa')).toBeLessThan(4.5);
  });

  it('lightens a dark brand colour on dark backgrounds until it passes AA', () => {
    const adjusted = adjustToContrast('#1E3A8A', ['#05080c', '#0a0f16']);

    expect(adjusted).toBe('#6d7fb3');
    expect(contrastRatio('#6d7fb3', '#0a0f16')).toBeGreaterThanOrEqual(4.5);
  });

  it('gives up when the backgrounds are both very light and very dark', () => {
    expect(adjustToContrast('#777777', ['#000000', '#ffffff'])).toBeNull();
  });
});
