/**
 * billingApi.js — chamadas à API de billing e subscrição.
 *
 * Todos os endpoints requerem autenticação JWT de Personal Trainer.
 * O Bearer token é injectado automaticamente pelo interceptor do axiosConfig.
 */

import api from './axiosConfig';

/**
 * Devolve o estado actual da subscrição do Personal Trainer autenticado.
 *
 * @returns {Promise<{
 *   status: string,         — trialing | active | past_due | cancelled | trial_expired
 *   tier: string,           — free | starter | pro
 *   tier_label: string,     — "Free" | "Starter" | "Pro"
 *   monthly_eur: number,    — 0 | 20 | 40
 *   active_clients_count: number,
 *   max_clients: number | null,
 *   trial_end: string | null,
 *   current_period_end: string | null,
 *   can_add_client: boolean,
 *   upgrade_message: string
 * }>}
 */

export const getSubscription = async () => {
  const response = await api.get('/api/v1/billing/subscription');
  return response.data;
};

/**
 * Gera uma URL do Stripe Checkout para o Personal Trainer adicionar método de pagamento.
 * Redireciona o utilizador para a página Stripe hospedada.
 *
 * @returns {Promise<{ checkout_url: string }>}
 */
export const createCheckout = async () => {
  const response = await api.post('/api/v1/billing/checkout');
  return response.data;
};

/**
 * Gera uma URL do Stripe Billing Portal para o Personal Trainer gerir a subscrição.
 * Permite cancelar, fazer upgrade/downgrade, e ver histórico de faturas.
 *
 * @returns {Promise<{ portal_url: string }>}
 */
export const createBillingPortal = async () => {
  const response = await api.post('/api/v1/billing/portal');
  return response.data;
};
