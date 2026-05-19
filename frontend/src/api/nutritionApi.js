/**
 * nutritionApi.js — camada de acesso à API para nutrição.
 *
 *  * Cobre dois domínios:
 *   1. Catálogo de alimentos (Foods)    — CRUD para o Personal Trainer
 *   2. Planos alimentares (MealPlans)   — listagem, criação, arquivo
 *
 */

import api from './axiosConfig';

// ============================================================
// CATÁLOGO DE ALIMENTOS
// ============================================================

/**
 * Lista alimentos visíveis para o Personal Trainer:
 *   - Globais (owner_trainer_id = null) — partilhados por todos
 *   - Privados do Personal Trainer autenticado
 *
 * @param {boolean} activeOnly - Se true, retorna apenas alimentos ativos (default: true)
 * @returns {Promise<Array>} Lista de alimentos com macros e kcal por 100g
 */
export const getFoods = async (activeOnly = true) => {
  const response = await api.get('/api/v1/nutrition/foods/', {
    params: { active: activeOnly },
  });
  return response.data;
};

/**
 * Cria um novo alimento privado para o Personal Trainer autenticado.
 * O backend define automaticamente owner_trainer_id = current_user.id.
 *
 * @param {Object} data - { name, carbs, protein, fats }
 * @returns {Promise<Object>} Alimento criado com kcal calculada
 */
export const createFood = async (data) => {
  const response = await api.post('/api/v1/nutrition/foods/', data);
  return response.data;
};

/**
 * Atualiza um alimento privado do Personal Trainer.
 * O backend rejeita com 403 se o alimento for global ou de outro Personal Trainer.
 *
 * @param {string} foodId   - UUID do alimento
 * @param {Object} data     - Campos a atualizar (todos opcionais)
 * @returns {Promise<Object>} Alimento atualizado com kcal recalculada
 */
export const updateFood = async (foodId, data) => {
  const response = await api.put(`/api/v1/nutrition/foods/${foodId}/`, data);
  return response.data;
};

/**
 * Desativa um alimento privado (soft delete — is_active = false).
 * O backend rejeita com 403 se o alimento for global.
 *
 * @param {string} foodId - UUID do alimento
 * @returns {Promise<void>}
 */
export const deleteFood = async (foodId) => {
  await api.delete(`/api/v1/nutrition/foods/${foodId}/`);
};

// ============================================================
// PLANOS ALIMENTARES
// ============================================================

/**
 * Lista os planos alimentares de um cliente específico.
 * Devolve apenas os planos não arquivados por omissão.
 *
 * Estrutura da resposta (MealPlanRead):
 *   id, client_id, name, plan_type, plan_type_label,
 *   starts_date, ends_date, active, notes,
 *   kcal_target, protein_target_g, carbs_target_g, fats_target_g,
 *   meals: [{ id, name, order_index, items: [...], meal_macros }],
 *   plan_macros: { protein_g, carbs_g, fats_g, kcal },
 *   adherence, archived_at, created_at, updated_at
 *
 * @param {string} clientId - UUID do cliente
 * @param {boolean} includeArchived - Incluir planos arquivados (default: false)
 * @returns {Promise<Array<MealPlanRead>>}
 */

export const getMealPlansByClient = async (
  clientId,
  includeArchived = false
) => {
  const response = await api.get(
    `/api/v1/nutrition/meal-plans/client/${clientId}`,
    {
      params: { include_archived: includeArchived },
    }
  );
  return response.data;
};

/**
 * Cria um plano alimentar completo (cabeçalho + refeições + alimentos).
 *
 * Estrutura do payload (MealPlanCreate):
 * {
 *   client_id: string,
 *   name: string,
 *   active: boolean,          // default: true (desactiva planos anteriores)
 *   notes?: string,
 *   starts_date?: string,     // YYYY-MM-DD
 *   ends_date?: string,
 *   kcal_target?: number,
 *   protein_target_g?: number,
 *   carbs_target_g?: number,
 *   fats_target_g?: number,
 *   meals: [
 *     {
 *       name: string,
 *       order_index: number,
 *       items: [
 *         { food_id: string, quantity_grams: number }
 *       ]
 *     }
 *   ]
 * }
 *
 * @param {Object} data - Payload conforme MealPlanCreate
 * @returns {Promise<MealPlanRead>} Plano criado com macros calculados
 */

export const createMealPlan = async (data) => {
  const response = await api.post('/api/v1/nutrition/meal-plans/', data);
  return response.data;
};

/**
 * Arquiva um plano alimentar (soft delete).
 * O plano não é apagado da BD — fica acessível via includeArchived=true.
 *
 * @param {string} planId - UUID do plano alimentar
 * @returns {Promise<void>}
 */
export const archiveMealPlan = async (planId) => {
  await api.delete(`/api/v1/nutrition/meal-plans/${planId}`);
};

/**
 * Reverte o arquivamento de um plano alimentar.
 * O plano volta a aparecer na lista de ativos (active=true, archived_at=null).
 * Não desativa outros planos — múltiplos ativos são permitidos.
 *
 * @param {string} planId - UUID do plano alimentar
 * @returns {Promise<MealPlanRead>} Plano reativado
 */
export const unarchiveMealPlan = async (planId) => {
  const response = await api.patch(
    `/api/v1/nutrition/meal-plans/${planId}/unarchive`
  );
  return response.data;
};

/**
 * Substitui todas as refeições de um plano alimentar existente (NR-03).
 *
 * Envia a lista completa de refeições no novo estado desejado.
 * O backend remove as refeições existentes e recria a partir deste payload.
 *
 * Payload (MealPlanMealsUpdate):
 * {
 *   meals: [
 *     {
 *       name: string,
 *       order_index: number,
 *       items: [{ food_id: string, quantity_grams: number }]
 *     }
 *   ]
 * }
 *
 * @param {string} planId  - UUID do plano alimentar
 * @param {Object} payload - { meals: [...] }
 * @returns {Promise<MealPlanRead>} Plano actualizado com macros recalculados
 */
export const updateMealPlanMeals = async (planId, payload) => {
  const response = await api.put(
    `/api/v1/nutrition/meal-plans/${planId}/meals`,
    payload
  );
  return response.data;
};

// ============================================================
// CALCULADORA DE MACROS
// ============================================================

/**
 * Obtém os factores de actividade física disponíveis.
 * Usado para popular o select de nível de actividade na calculadora.
 *
 * @returns {Promise<Array<{key, label, factor, description}>>}
 */

export const getActivityFactors = async () => {
  const response = await api.get('/api/v1/nutrition/activity-factors/');
  return response.data;
};

/**
 * Calcula TMB/TDEE pelas 3 fórmulas e opcionalmente distribui macros.
 *
 * Payload mínimo (só TMB/TDEE):
 * {
 *   weight_kg: number,  height_cm: number,
 *   age: number,        sex: "male" | "female",
 *   activity_key: string
 * }
 *
 * Payload completo (TMB + distribuição de macros por percentagens):
 * {
 *   ...dados biométricos,
 *   kcal_target: number,
 *   method: "percentages",
 *   protein_pct: number,  carbs_pct: number,  fats_pct: number
 * }
 *
 * Payload completo (TMB + distribuição de macros por g/kg):
 * {
 *   ...dados biométricos,
 *   kcal_target: number,
 *   method: "grams_per_kg",
 *   protein_g_per_kg: number,  carbs_g_per_kg: number,  fats_g_per_kg: number
 * }
 *
 * Resposta (MacroCalculationResponse):
 * {
 *   weight_kg, height_cm, age, sex, activity_key, activity_label, activity_factor,
 *   formulas: [{ formula, label, tmb, tdee }],  // 3 resultados, ordem crescente de TDEE
 *   macro_distribution: { ... } | null           // null se kcal_target não enviado
 * }
 *
 * @param {Object} payload
 * @returns {Promise<MacroCalculationResponse>}
 */
export const calculateMacros = async (payload) => {
  const response = await api.post(
    '/api/v1/nutrition/calculate-macros/',
    payload
  );
  return response.data;
};
