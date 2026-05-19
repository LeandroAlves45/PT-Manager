/**
 * handlers.js — interceptores HTTP para os testes (MSW).
 *
 * O MSW (Mock Service Worker) intercepta os pedidos Axios ao nível da rede,
 * antes de chegarem ao servidor real. Isto significa que os componentes usam
 * o axiosConfig.js real — os testes são mais fiéis ao comportamento em produção.
 *
 * Convenção de respostas:
 *   - Respostas de sucesso reflectem a estrutura real da API FastAPI
 *   - IDs são UUIDs fixos para facilitar asserções
 *   - Dados em português (Portugal) como na app real
 *
 * IMPORTANTE: MSW v2 no Node.js (setupServer) exige URLs absolutas.
 * O BASE_URL deve corresponder ao VITE_API_BASE_URL definido em vitest.config.js.
 */

import { http, HttpResponse } from 'msw';

const BASE = 'http://localhost:8000';

// Dados de testes partilhados

export const TEST_IDS = {
  trainer: 'trainer-uuid-001',
  client: 'client-uuid-001',
  clientRecord: 'client-record-uuid-001',
  supplement: 'supplement-uuid-001',
};

export const TEST_TOKEN = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.test_token';

// Fixtures de resposta

export const TRAINER_USER = {
  id: TEST_IDS.trainer,
  email: 'trainer@teste.pt',
  full_name: 'Trainer Teste',
  role: 'trainer',
  client_id: null,
  is_active: true,
  created_at: '2025-01-01T00:00:00Z',
  updated_at: '2025-01-01T00:00:00Z',
};

export const CLIENT_USER = {
  id: TEST_IDS.client,
  email: 'cliente@teste.pt',
  full_name: 'Cliente Teste',
  role: 'client',
  client_id: TEST_IDS.clientRecord,
  is_active: true,
  created_at: '2025-01-01T00:00:00Z',
  updated_at: '2025-01-01T00:00:00Z',
};

export const TRAINER_SETTINGS = {
  primary_color: '#00A8E8',
  logo_url: null,
  app_name: 'PT Manager Teste',
};

// Handlers HTTP por dominio

export const handlers = [
  // Autenticação
  http.post(`${BASE}/api/v1/auth/login`, async ({ request }) => {
    const { email, password } = await request.json();

    // Simula autenticação — aceita apenas as credenciais de teste
    if (email === 'trainer@teste.pt' && password === 'Trainer123!') {
      return HttpResponse.json({
        access_token: TEST_TOKEN,
        token_type: 'bearer',
        role: 'trainer',
        user_id: TEST_IDS.trainer,
        full_name: 'Trainer Teste',
      });
    }

    if (email === 'cliente@teste.pt' && password === 'Cliente123!') {
      return HttpResponse.json({
        access_token: TEST_TOKEN,
        token_type: 'bearer',
        role: 'client',
        user_id: TEST_IDS.client,
        full_name: 'Cliente Teste',
      });
    }

    // Credenciais inválidas
    return HttpResponse.json(
      { detail: 'Email ou password inválidos' },
      { status: 401 }
    );
  }),

  http.get(`${BASE}/api/v1/auth/users/me`, () => {
    // Devolve o utilizador com base no token de teste
    return HttpResponse.json(TRAINER_USER);
  }),

  http.post(`${BASE}/api/v1/auth/logout`, () => {
    return HttpResponse.json({ detail: 'Logout bem-sucedido' });
  }),

  // Branding do Personal Trainer
  http.get(`${BASE}/api/v1/trainer-profile/settings`, () => {
    return HttpResponse.json(TRAINER_SETTINGS);
  }),

  // Convite de cliente
  http.get(`${BASE}/api/v1/invites/validate/:token`, ({ params }) => {
    const { token } = params;

    if (token === 'token-valido-123') {
      return HttpResponse.json({
        valid: true,
        client_name: 'João Silva',
        message: '',
      });
    }

    if (token === 'token-expirado-456') {
      return HttpResponse.json({
        valid: false,
        client_name: '',
        message: 'O link de convite expirou. Contacta o seu Personal Trainer.',
      });
    }

    return HttpResponse.json({
      valid: false,
      client_name: '',
      message:
        'Link de convite inválido ou expirado. Contacta o seu Personal Trainer.',
    });
  }),

  http.post(
    `${BASE}/api/v1/invites/set-password/:token`,
    async ({ request, params }) => {
      const { token } = params;
      const { new_password } = await request.json();

      if (token !== 'token-valido-123') {
        return HttpResponse.json(
          {
            detail:
              'Link de convite inválido ou expirado. Contacta o seu Personal Trainer.',
          },
          { status: 400 }
        );
      }

      if (new_password.length < 6) {
        return HttpResponse.json(
          {
            detail: [
              {
                loc: ['body', 'new_password'],
                msg: 'too short',
                type: 'string_too_short',
              },
            ],
          },
          { status: 400 }
        );
      }

      return HttpResponse.json({
        access_token: TEST_TOKEN,
        token_type: 'bearer',
        role: 'client',
        user_id: TEST_IDS.client,
        full_name: 'João Silva',
      });
    }
  ),

  // Suplementos
  http.get(`${BASE}/api/v1/supplements`, () => {
    return HttpResponse.json([
      {
        id: TEST_IDS.supplement,
        name: 'Creatina Monohidratada',
        description:
          'Suplemento para melhorar a performance e recuperação muscular.',
        serving_size: '5g',
        timing: 'Pós-treino',
        trainer_notes:
          'Recomenda-se tomar diariamente, mesmo nos dias de descanso.',
        archived_at: null,
        created_by_user_id: TEST_IDS.trainer,
        created_at: '2025-01-01T00:00:00Z',
        updated_at: '2025-01-01T00:00:00Z',
      },
    ]);
  }),

  http.post(`${BASE}/api/v1/supplements`, async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json(
      {
        id: 'new-supplement-uuid',
        ...body,
        archived_at: null,
        created_by_user_id: TEST_IDS.trainer,
        created_at: new Date().toISOString(),
        updated_at: new Date().toISOString(),
      },
      { status: 201 }
    );
  }),
];
