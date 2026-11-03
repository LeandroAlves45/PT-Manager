import { describe, expect, it } from 'vitest';

import {
  externalVideoLink,
  isValidExternalVideoUrl,
} from '@/features/exercise-video/lib/externalVideo';

describe('externalVideoLink', () => {
  it.each([
    ['https://www.youtube.com/watch?v=dQw4w9WgXcQ', 'YouTube'],
    ['https://youtube.com/shorts/dQw4w9WgXcQ', 'YouTube'],
    ['https://m.youtube.com/watch?v=dQw4w9WgXcQ', 'YouTube'],
    ['  https://youtu.be/dQw4w9WgXcQ?t=30  ', 'YouTube'],
    ['https://WWW.YOUTUBE.COM/watch?v=dQw4w9WgXcQ', 'YouTube'],
    ['https://vimeo.com/76979871', 'Vimeo'],
    ['https://www.vimeo.com/76979871', 'Vimeo'],
  ])('accepts %s as a %s link', (value, provider) => {
    expect(externalVideoLink(value)?.provider).toBe(provider);
  });

  it.each([
    null,
    undefined,
    '',
    '   ',
    'http://www.youtube.com/watch?v=x',
    'javascript:alert(1)',
    'www.youtube.com/watch?v=x',
    'https://youtube.com.evil.example/watch?v=x',
    'https://evil.example/youtube.com',
    'https://www.youtube.com@evil.example/watch?v=x',
    'https://user:pass@www.youtube.com/watch?v=x',
    'https://www.youtube.com:8443/watch?v=x',
    'https://drive.google.com/file/d/x',
    `https://youtu.be/${'a'.repeat(490)}`,
  ])('refuses %s', (value) => {
    expect(externalVideoLink(value)).toBeNull();
  });

  it('returns the normalised address to open', () => {
    expect(externalVideoLink(' https://YOUTU.BE/abc ')?.href).toBe('https://youtu.be/abc');
  });
});

describe('isValidExternalVideoUrl', () => {
  it('accepts an empty field and refuses a link from another site', () => {
    expect(isValidExternalVideoUrl('')).toBe(true);
    expect(isValidExternalVideoUrl('https://youtu.be/abc')).toBe(true);
    expect(isValidExternalVideoUrl('https://example.com/video')).toBe(false);
  });
});
