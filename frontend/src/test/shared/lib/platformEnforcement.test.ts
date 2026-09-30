import { describe, expect, it } from 'vitest';

import { platformEnforcementReasonLabel } from '@/shared/lib/platformEnforcement';

describe('platformEnforcementReasonLabel', () => {
  it('labels a known reason in Portuguese', () => {
    expect(platformEnforcementReasonLabel('dangerous_information')).toBe('Informação perigosa');
  });

  // [Fecho 6E-3] `reason in OBJ` aceitava chaves do protótipo ("constructor" dava uma função).
  it.each([null, 'unknown_reason', 'constructor', 'toString'])('shows "—" for %s', (reason) => {
    expect(platformEnforcementReasonLabel(reason)).toBe('—');
  });
});
