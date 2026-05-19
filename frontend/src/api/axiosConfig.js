/**
 * axiosConfig.js — instância Axios centralizada para toda a aplicação.
 *
 * Esta é a única instância Axios que deve ser usada em toda a app.
 * Nunca usar fetch() ou criar outra instância Axios directamente.
 *
 * Configuração incluída:
 *   - Base URL (Render em produção, localhost em desenvolvimento)
 *   - Header X-API-Key em todos os pedidos
 *   - Interceptor de request: injeta Bearer token JWT se existir
 *   - Interceptor de response (401):
 *       1. Tenta POST /auth/refresh com o token actual
 *       2. Se o refresh tiver sucesso: guarda o novo token, repete o pedido original
 *       3. Se o refresh falhar: limpa localStorage e redireciona para /login
 *   - Fila de pedidos concorrentes: se vários pedidos falham em simultâneo
 *     com 401, apenas um refresh é feito — os outros esperam e reutilizam
 *     o novo token
 */

import axios from 'axios';

// ============================================================
// Configuração base
// ============================================================
const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  (import.meta.env.MODE === 'production'
    ? 'https://pt-manager-back-end.onrender.com' // Produção
    : 'http://localhost:8000'); // Desenvolvimento local

const API_KEY = import.meta.env.VITE_API_KEY;

// ============================================================
// Criação da instância Axios com configuração base
// ============================================================

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 60000, // Tempo limite de 60 segundos
  headers: {
    'Content-Type': 'application/json',
    ...(API_KEY && { 'X-API-KEY': API_KEY }),
  },
});

// ============================================================
// Estado do mecanismo de refresh
//
// isRefreshing: true enquanto o pedido POST /auth/refresh está em curso.
//   Impede que múltiplos 401 simultâneos disparem múltiplos refreshes.
//
// failedQueue: array de pedidos que chegaram com 401 enquanto o refresh
//   já estava em curso. Cada entrada tem resolve/reject.
//   Quando o refresh termina:
//     - Sucesso → todos os pedidos na fila são resolvidos com o novo token
//     - Falha   → todos os pedidos na fila são rejeitados
// ============================================================

let isRefreshing = false;
let failedQueue = [];

/**
 * Processa a fila de pedidos que ficaram à espera durante o refresh.
 *
 * @param {Error|null} error  — se null, o refresh teve sucesso
 * @param {string|null} token — o novo token (só presente em caso de sucesso)
 */
function processQueue(error, token = null) {
  failedQueue.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else {
      resolve(token);
    }
  });
  failedQueue = [];
}

// ============================================================
// Interceptor de REQUEST — injeta o Bearer token JWT
// ============================================================

api.interceptors.request.use(
  (config) => {
    // Lê o token guardado no login (AuthContext.login guarda aqui)
    const token = localStorage.getItem('pt_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    if (import.meta.env.MODE === 'development') {
      console.log(`📤 ${config.method.toUpperCase()} ${config.url}`);
    }
    return config;
  },
  (error) => {
    console.error('Erro na configuração do pedido:', error);
    return Promise.reject(error);
  }
);

// ============================================================
// Interceptor de RESPONSE — tratamento de erros
// ============================================================

api.interceptors.response.use(
  // Repostas com sucesso (2xx) passam direto
  (response) => response,

  async (error) => {
    if (!error.response) {
      // Sem resposta do servidor (ex: rede offline)
      console.error('❌ Erro de rede ou timeout:', error.message);
      return Promise.reject(error);
    }

    const { status, data } = error.response;
    // Pedido original que falhou
    const originalRequest = error.config;

    if (
      status === 401 &&
      !originalRequest._retry && // Evita loop infinito de refresh
      !originalRequest.url?.includes('/auth/refresh') && // Não tenta refresh se o próprio refresh falhou
      window.location.pathname !== '/login' // Evita redirecionar para login se já estamos lá
    ) {
      // Marcar o pedido original para evitar múltiplos refreshes
      originalRequest._retry = true;

      if (isRefreshing) {
        // Já está um refresh em curso, colocar este pedido na fila
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((newToken) => {
            // Refresh teve sucess, repetir o pedido original com o novo token
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
            return api(originalRequest);
          })
          .catch((err) => {
            return Promise.reject(err);
          });
      }

      // Este é o primeiro pedido a falhrar com 401, iniciar o processo de refresh
      isRefreshing = true;

      const currentToken = localStorage.getItem('pt_token');

      try {
        // POST/auth/refresh com o token atual
        const refreshResponse = await axios.post(
          `${API_BASE_URL}/auth/refresh`,
          {},
          {
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${currentToken}`,
              ...(API_KEY && { 'X-API-KEY': API_KEY }),
            },
            // Timeout mais curto para o refresh
            timeout: 10000,
          }
        );

        const newToken = refreshResponse.data.access_token;

        // Guardar o novo token no localStorage
        localStorage.setItem('pt_token', newToken);

        // Notificar todos os pedidos que estavam à espera do refresh
        processQueue(null, newToken);

        // Repetir o pedido original com o novo token
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return api(originalRequest);
      } catch (refreshError) {
        // Refresh falhou — sessão verdadeiramente expirada ou revogada
        // (logout noutro dispositivo, token inválido, servidor indisponível)
        processQueue(refreshError, null);

        // Limpar localStorage e redirecionar para login
        localStorage.removeItem('pt_token');

        // Redirecionar para /login se não estivermos já lá
        if (window.location.pathname !== '/login') {
          window.location.href = '/login';
        }

        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    // ----------------------------------------------------------
    // Outros erros HTTP — log e propagação
    // ----------------------------------------------------------

    if (status === 422) {
      // Erro de validação Pydantic — mostrar mensagens de erro detalhadas
      console.error('❌ Erro 422 — Validação falhou:');
      if (data.detail && Array.isArray(data.detail)) {
        data.detail.forEach((err) => {
          console.error(
            `  Campo: ${err.loc.join(' → ')} | Erro: ${err.msg} | Tipo: ${err.type}`
          );
        });
      } else {
        console.error(JSON.stringify(data, null, 2));
      }
    } else if (status === 402) {
      // Subscrição expirada
      console.warn(
        '⚠️ Erro 402 — Subscrição expirada ou inativa',
        data?.detail
      );
    } else {
      // Outros erros — log genérico
      console.error(`❌ Erro ${status}`, data);
    }

    return Promise.reject(error);
  }
);

export default api;
