import { describe, expect, it } from 'vitest';

import { describeExerciseVideo } from '@/features/admin-catalog/lib/exerciseVideo';

describe('describeExerciseVideo', () => {
  it.each([
    [null, false, null, 'Sem vídeo'],
    [null, false, 'https://example.com/v', 'Ligação de vídeo externa'],
    ['ready', true, null, 'Vídeo pronto'],
    ['processing', false, null, 'Vídeo em processamento'],
    ['processing', true, null, 'Vídeo disponível · substituição em processamento'],
    ['pending', true, null, 'Vídeo disponível · substituição pendente'],
    ['rejected', true, null, 'Vídeo disponível · última substituição recusado'],
    ['failed', false, null, 'Vídeo falhado'],
  ] as const)('%s with ready=%s and link=%s reads "%s"', (status, ready, link, expected) => {
    expect(
      describeExerciseVideo({
        managed_video_status: status,
        has_ready_video: ready,
        video_url: link,
      })
    ).toBe(expected);
  });
});
