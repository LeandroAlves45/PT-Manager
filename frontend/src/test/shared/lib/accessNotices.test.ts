import { describe, expect, it } from 'vitest';

import { accessCutMessage, trainerBlockedMessage } from '@/shared/lib/accessNotices';

describe('trainerBlockedMessage', () => {
  it('shows the reason label and the note', () => {
    expect(trainerBlockedMessage('dangerous_information', ' Doses perigosas. ')).toBe(
      'A tua conta está bloqueada. Motivo: Informação perigosa. Nota: Doses perigosas.'
    );
  });

  it('leaves out a reason the app does not know instead of writing «Motivo: —»', () => {
    expect(trainerBlockedMessage('something_new', null)).toBe('A tua conta está bloqueada.');
  });

  it('leaves out an empty note', () => {
    expect(trainerBlockedMessage('prohibited_content', '   ')).toBe(
      'A tua conta está bloqueada. Motivo: Conteúdo proibido.'
    );
  });
});

describe('accessCutMessage', () => {
  it('maps only the three access cut codes', () => {
    expect(accessCutMessage('authentication_session_ended', null, null)).toBe(
      'A tua sessão terminou. Entra novamente.'
    );
    expect(accessCutMessage('client_access_period_ended', 'malicious_content', 'x')).toBe(
      'O prazo de acesso terminou.'
    );
    expect(accessCutMessage('authentication_invalid_credentials', null, null)).toBeNull();
  });
});
