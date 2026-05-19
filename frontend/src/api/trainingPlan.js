import api from './axiosConfig';

// ============================================================================
// TRAINING PLANS API - Gestão de Planos de Treino
// ============================================================================

/**
 * Lista todos os planos de treino.
 * O backend aceita filtros por query string: Client_id, Status, Page_size, Page_number
 */

export const getTrainingPlans = async (params = {}) => {
  const response = await api.get('/api/v1/training_plans/', { params });
  return response.data;
};

/**
 * * Cria um novo plano de treino.
 *
 * @param {Object} data - Dados do plano
 * @param {string} [data.client_id] - ID do cliente (null para templates)
 * @param {string} data.name - Nome do plano
 * @param {string} [data.status] - Status (draft, published, archived)
 * @param {string} [data.start_date] - Data início
 * @param {string} [data.end_date] - Data fim
 * @param {string} [data.notes] - Notas adicionais
 *
 * @returns {Promise<Object>} Plano criado
 */

export const createTrainingPlan = async (data) => {
  const response = await api.post('/api/v1/training_plans/', data);
  return response.data;
};

/**
 * Atualiza um plano de treino existente.
 *
 * @param {string} planId - ID do plano
 * @param {Object} data - Dados a atualizar
 * @returns {Promise<Object>} Plano atualizado
 */

export const updateTrainingPlan = async (planId, data) => {
  const response = await api.put(`/api/v1/training_plans/${planId}/`, data);
  return response.data;
};

/**
 * Remove um plano de treino.
 *
 * @param {string} planId - ID do plano
 * @returns {Promise<void>}
 */

export const deleteTrainingPlan = async (planId) => {
  await api.delete(`/api/v1/training_plans/${planId}`);
};

// ============================================================================
// PLAN DAYS - Dias do Plano
// ============================================================================

/**
 * Lista todos os dias de um plano de treino.
 *
 * @param {string} planId - ID do plano
 * @returns {Promise<Array>} Lista de dias
 */

export const getPlanDays = async (planId) => {
  const response = await api.get(`/api/v1/training_plans/${planId}/days`);
  return response.data;
};

/**
 * Adiciona um novo dia ao plano.
 *
 * @param {string} planId - ID do plano
 * @param {Object} data - Dados do dia
 * @param {string} data.name - Nome do dia (ex: "Treino A - Peito/Tríceps")
 * @param {number} data.order_index - Ordem do dia
 * @param {string} [data.notes] - Notas
 *
 * @returns {Promise<Object>} Dia criado
 */

export const createPlanDay = async (planId, data) => {
  const response = await api.post(
    `/api/v1/training_plans/${planId}/days`,
    data
  );
  return response.data;
};

/**
 * Atualiza um dia do plano.
 *
 * @param {string} dayId - ID do dia
 * @param {Object} data - Dados a atualizar
 * @returns {Promise<Object>} Dia atualizado
 */

export const updatePlanDay = async (dayId, data) => {
  const response = await api.put(`/api/v1/training_plans/days/${dayId}`, data);
  return response.data;
};

/**
 * Remove um dia do plano.
 *
 * @param {string} dayId - ID do dia
 * @returns {Promise<void>}
 */

export const deletePlanDay = async (dayId) => {
  await api.delete(`/api/v1/training_plans/days/${dayId}`);
};

// ============================================================================
// DAY EXERCISES - Exercícios do Dia
// ============================================================================

/**
 * Lista exercícios de um dia específico.
 *
 * @param {string} planId - ID do plano
 * @param {string} dayId - ID do dia
 * @returns {Promise<Array>} Lista de exercícios
 */

export const getDayExercises = async (dayId) => {
  const response = await api.get(
    `/api/v1/training_plans/days/${dayId}/exercises`
  );
  return response.data;
};

/**
 * Adiciona exercício a um dia.
 *
 * @param {string} dayId - ID do dia
 * @param {Object} data - Dados do exercício
 * @param {string} data.exercise_id - ID do exercício
 * @param {number} data.order_index - Ordem do exercício
 * @param {number} data.sets - Número de séries
 * @param {string} data.reps_range - Faixa de repetições (ex: "8-12")
 * @param {string} [data.rest_range_seconds] - Descanso (ex: "60-90")
 * @param {string} [data.tempo] - Tempo de execução (ex: "3-1-1-0")
 * @param {number} [data.is_superset_group] - Grupo de superset
 * @param {boolean} [data.substitution_allowed] - Permite substituição
 *
 * @returns {Promise<Object>} Exercício adicionado
 */
export const addExerciseToDay = async (dayId, data) => {
  const response = await api.post(
    `/api/v1/training_plans/days/${dayId}/exercises`,
    data
  );
  return response.data;
};

/**
 * Atualiza exercício de um dia.
 *
 * @param {string} dayId - ID do dia
 * @param {string} exerciseId - ID do exercício no dia
 * @param {Object} data - Dados a atualizar
 * @returns {Promise<Object>} Exercício atualizado
 */

export const updateDayExercise = async (dayExerciseId, data) => {
  const response = await api.put(
    `/api/v1/training_plans/days/exercises/${dayExerciseId}`,
    data
  );
  return response.data;
};

/**
 * Remove exercício de um dia.
 *
 * @param {string} dayExerciseId - ID do exercício no dia
 * @returns {Promise<void>}
 */

export const deleteDayExercise = async (dayExerciseId) => {
  await api.delete(`/api/v1/training_plans/days/exercises/${dayExerciseId}`);
};

// ============================================================================
// SET LOADS - Cargas por Série
// ============================================================================

/**
 * Lista cargas de todas as séries de um exercício.
 *
 * @param {string} dayExerciseId - ID do exercício no dia
 * @returns {Promise<Array>} Lista de cargas por série
 */

export const getExerciseSetLoads = async (dayExerciseId) => {
  const response = await api.get(
    `/api/v1/training_plans/days/exercises/${dayExerciseId}/loads`
  );
  return response.data;
};

/**
 * Define carga para uma série específica.
 *
 * @param {string} dayExerciseId - ID do exercício no dia
 * @param {string} dayId - ID do dia
 * @param {string} exerciseId - ID do exercício no dia
 * @param {Object} data - Dados da carga
 * @param {number} data.set_number - Número da série (1, 2, 3...)
 * @param {number} data.load_kg - Carga em kg
 * @param {string} [data.notes] - Notas
 *
 * @returns {Promise<Object>} Carga criada/atualizada
 */
export const setExerciseSetLoad = async (dayExerciseId, data) => {
  const response = await api.post(
    `/api/v1/training_plans/days/exercises/${dayExerciseId}/loads`,
    data
  );
  return response.data;
};

/**
 * Atualiza carga de uma série.
 *
 * @param {string} dayExerciseId - ID do exercício no dia
 * @param {string} loadId - ID da carga
 * @param {Object} data - Dados a atualizar
 * @returns {Promise<Object>} Carga atualizada
 */

export const updateExerciseSetLoad = async (setLoadId, data) => {
  const response = await api.put(
    `/api/v1/training_plans/days/exercises/loads/${setLoadId}`,
    data
  );
  return response.data;
};

/**
 * Remove carga de uma série.
 *
 * @param {string} setLoadId - ID da carga
 * @returns {Promise<void>}
 */

export const deleteExerciseSetLoad = async (setLoadId) => {
  await api.delete(`/api/v1/training_plans/set-loads/${setLoadId}`);
};

// ============================================================================
// CLIENT ACTIVE PLAN - Plano Ativo do Cliente
// ============================================================================

/**
 * Busca o plano ativo atual de um cliente.
 *
 * @param {string} clientId - ID do cliente
 * @returns {Promise<Object|null>} Plano ativo ou null
 */

export const getClientActivePlan = async (clientId) => {
  const response = await api.get(
    `/api/v1/training_plans/active-plan/${clientId}`
  );
  return response.data;
};

/**
 * Define um plano como ativo para um cliente.
 *
 * @param {Object} data - Dados
 * @param {string} data.client_id - ID do cliente
 * @param {string} data.training_plan_id - ID do plano
 * @param {string} [data.active_from] - Data início (padrão: hoje)
 *
 * @returns {Promise<Object>} Plano ativo criado
 */
export const setClientActivePlan = async (data) => {
  const response = await api.post('/api/v1/training_plans/active-plan', data);
  return response.data;
};

/**
 * Encerra o plano ativo de um cliente.
 *
 * @param {string} clientId - ID do cliente
 * @returns {Promise<Object|null>} Plano encerrado
 */

export const closeClientActivePlan = async (clientId) => {
  const response = await api.post(
    `/api/v1/training_plans/clients/${clientId}/active/close`
  );
  return response.data;
};

// ============================================================================
// CLONE PLAN - Clonar Plano Template para Cliente
// ============================================================================

/**
 * Clona um plano template para um cliente específico.
 *
 * Útil para:
 * - Criar bibliotecas de templates
 * - Reutilizar planos entre clientes
 * - Personalizar planos base
 *
 * @param {string} templatePlanId - ID do template
 * @param {Object} data - Dados da clonagem
 * @param {string} data.client_id - ID do cliente destino
 * @param {string} [data.name] - Nome do novo plano (padrão: nome do template)
 * @param {boolean} [data.activate] - Ativar plano após clonar
 * @param {string} [data.activate_from] - Data de ativação
 *
 * @returns {Promise<Object>} Novo plano criado
 */

export const clonePlanToClient = async (templatePlanId, data) => {
  const response = await api.post(
    `/api/v1/training_plans/${templatePlanId}/clone-to-client/${data.client_id}`,
    data
  );
  return response.data;
};
