/**
 * helpers.test.js — testes das funções utilitárias puras.
 *
 * Funções puras (sem DOM, sem rede, sem estado) — os testes
 * mais rápidos de escrever e os mais estáveis. São executados
 * em milissegundos e nunca precisam de mocks.
 */

import {
  formatDate,
  formatDateTime,
  formatTime,
  formatRelativeDate,
  calculateAge,
  getInitials,
  getStatusColor,
  getStatusLabel,
  getSexLabel,
} from '@/lib/helpers';
import test, { describe } from 'node:test';

// formatDate

describe('formatDate', () => {
  test('formata data ISO para "DD/MM/YYYY"', () => {
    // A data '2025-03-15' deve aparecer como '15/03/2025'
    expect(formatDate('2025-03-15')).toBe('15/03/2025');
  });

  test('devolve string vazia para null', () => {
    // Sem data -> não deve rebentar, apenas devolver string vazia
    expect(formatDate(null)).toBe('');
  });

  test('devolve string vazia para undefined', () => {
    // Sem data -> não deve rebentar, apenas devolver undefined
    expect(formatDate(undefined)).toBe('');
  });

  test('devolve string vazia para string vazia', () => {
    expect(formatDate('')).toBe('');
  });

  test('formata corretamente o início do ano', () => {
    expect(formatDate('2025-01-01')).toBe('01/01/2025');
  });

  test('formata corretamente o fim do ano', () => {
    expect(formatDate('2025-12-31')).toBe('31/12/2025');
  });
});

// formatDateTime

describe('formatDateTime', () => {
  test('devolve string vazia para null', () => {
    expect(formatDateTime(null)).toBe('');
  });

  test('resultado contém data e hora', () => {
    const result = formatDateTime('2025-03-15T14:30:00Z');
    // Deve conter o ano para confirmar que é uma data/hora válida
    expect(result).toContain('2025');
  });
});

// formatTime

describe('formatTime', () => {
  test('devolve string vazia para null', () => {
    expect(formatTime(null)).toBe('');
  });

  test('extrai hora e minutos de um timestamp', () => {
    // O resultado deve estar no formato "HH:mm"
    const result = formatTime('2025-03-15T14:30:00Z');
    expect(result).toMatch(/^\d{2}:\d{2}$/);
  });
});

// calculateAge

describe('calculateAge', () => {
  test('devolve null para null', () => {
    expect(calculateAge(null)).toBeNull();
  });

  test('calcula idade corretamente', () => {
    // Criar uma data de nascimento exatamente X anos atrás
    const today = new Date();
    const birthYear = today.getFullYear() - 30; // 30 anos atrás
    const birthDate = `${birthYear}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    expect(calculateAge(birthDate)).toBe(30);
  });

  test('não adiciona ano antes do aniversário', () => {
    // Se o aniversário é amanhã, ainda tem N-1 anos
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    const birthYear = tomorrow.getFullYear() - 25;
    const birthDate = `${birthYear}-${String(tomorrow.getMonth() + 1).padStart(2, '0')}-${String(tomorrow.getDate()).padStart(2, '0')}`;
    expect(calculateAge(birthDate)).toBe(24);
  });
});

// getInitials

describe('getInitials', () => {
  test('extrai iniciais de nome completo', () => {
    expect(getInitials('João Silva')).toBe('JS');
  });

  test('extrai iniciais de três nomes', () => {
    expect(getInitials('Ana Maria Costa')).toBe('AMC');
  });

  test('devolve ?? para null', () => {
    expect(getInitials(null)).toBe('??');
  });

  test('devolve ?? para string vazia', () => {
    expect(getInitials('')).toBe('??');
  });

  test('converte para maiúsculas', () => {
    expect(getInitials('joão silva')).toBe('JS');
  });

  test('ignora espaços extras', () => {
    expect(getInitials('  João   Silva  ')).toBe('JS');
  });
});

// getStatusColor

describe('getStatusColor', () => {
  test('devolve cor para status "scheduled"', () => {
    const result = getStatusColor('scheduled');
    // Deve ser uma string representando uma cor (ex: "#007bff")
    expect(result).toBeTruthy();
    expect(typeof result).toBe('string');
  });

  test('devolve cor para status "completed"', () => {
    const result = getStatusColor('completed');
    // Deve ser uma string representando uma cor (ex: "#28a745")
    expect(result).toBeTruthy();
  });

  test('completed e scheduled têm cores diferentes', () => {
    // Sessão concluida e agendada devem ter cores diferentes
    expect(getStatusColor('completed')).not.toBe(getStatusColor('scheduled'));
  });

  test('status desconhecido devolve cor de fallback', () => {
    // Não deve rebentar para status desconhecido, apenas devolver cor de fallback
    const result = getStatusColor('unknown-status');
    expect(result).toBeTruthy();
  });

  test('devolve cor para status "missed"', () => {
    const result = getStatusColor('missed');
    expect(result).toBeTruthy();
  });
});

// getStatusLabel

describe('getStatusLabel', () => {
  test('devolve "Agendada" para status "scheduled"', () => {
    expect(getStatusLabel('scheduled')).toBe('Agendada');
  });

  test('devolve "Concluída" para status "completed"', () => {
    expect(getStatusLabel('completed')).toBe('Concluída');
  });

  test('devolve "Arquivado" para status "archived"', () => {
    expect(getStatusLabel('archived')).toBe('Arquivado');
  });

  test('status desconhecido devolve o próprio status', () => {
    expect(getStatusLabel('unknown-status')).toBe('unknown-status');
  });
});

// getSexLabel

describe('getSexLabel', () => {
  test('devolve "Masculino" para "male"', () => {
    expect(getSexLabel('male')).toBe('Masculino');
  });

  test('devolve "Feminino" para "female"', () => {
    expect(getSexLabel('female')).toBe('Feminino');
  });

  test('devolve "-" para null', () => {
    expect(getSexLabel(null)).toBe('-');
  });

  test('devolve "-" para valor desconhecido', () => {
    expect(getSexLabel('outro_valor')).toBe('-');
  });
});
