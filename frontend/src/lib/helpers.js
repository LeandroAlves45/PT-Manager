/**
 * Helpers centralizados para formatação e lógica de apresentação
 */

// FORMATAÇÃO DE DATAS

/**
 * Formata uma data ISO para "DD/MM/YYYY"
 * @param {string} dateStr - Data em formato ISO (ex: "2024-06-01T12:00:00Z")
 * @returns {string} Data formatada (ex: "01/06/2024")
 **/
export function formatDate(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return date.toLocaleDateString('pt-PT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

/**
 * Formata uma data ISO para "DD/MM/YYYY HH:mm"
 * @param {string} dateStr - Data em formato ISO (ex: "2024-06-01T12:00:00Z")
 * @returns {string} Data formatada (ex: "01/06/2024 14:00")
 **/
export function formatDateTime(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return date.toLocaleString('pt-PT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Formata uma data ISO para "HH:mm"
 * @param {string} dateStr - Data em formato ISO (ex: "2024-06-01T12:00:00Z")
 * @returns {string} Hora formatada (ex: "14:00")
 **/
export function formatTime(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return date.toLocaleTimeString('pt-PT', {
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Formata data relativa (ex: "há 2 dias", "há 3 horas")
 * @param {string} dateStr - Data em formato ISO
 * @returns {string} Data relativa
 */
export function formatRelativeDate(dateStr) {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'agora mesmo';
  if (diffMins < 60) return `há ${diffMins} minutos`;
  if (diffHours < 24) return `há ${diffHours} horas`;
  if (diffDays < 7) return `há ${diffDays} dias`;
  return formatDate(dateStr);
}

/**
 * Calcula a idade a partir da data de nascimento
 * @param {string} birthDate - Data de nascimento em formato ISO
 * @returns {number} Idade em anos
 */
export function calculateAge(birthDate) {
  if (!birthDate) return null;
  const today = new Date();
  const birth = new Date(birthDate);
  let age = today.getFullYear() - birth.getFullYear();
  const monthDiff = today.getMonth() - birth.getMonth();
  //Se ainda não fez anos este ano, subtrai 1
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
    age--;
  }
  return age;
}

//INICIAIS E AVATAR

/**
 * Extrai as iniciais de um nome
 * @param {string} name - Nome completo
 * @returns {string} Iniciais (ex: "João Silva" => "JS")
 */
export function getInitials(name) {
  if (!name) return '??';
  return name
    .split(' ')
    .filter(Boolean) // Remove espaços extras
    .map((word) => word[0])
    .join('')
    .toUpperCase();
}

//STATUS - CORES E LABELS

/**
 * Retorna as classes CSS para badges de status
 * @param {string} status - Status da sessão (ex: "scheduled", "completed", "canceled")
 * @returns {string} Classes CSS para o badge
 */
export function getStatusColor(status) {
  const colors = {
    //status da sessão
    scheduled: 'bg-primary/15 text-primary border-primary/20',
    completed: 'bg-success/15 text-success border-success/20',
    canceled: 'bg-muted text-muted border-muted',
    missed: 'bg-destructive/15 text-destructive border-destructive/20',
    //status do cliente
    active: 'bg-success/15 text-success border-success/20',
    archived: 'bg-muted text-muted-foreground border-muted',
    //status do plano
    draft: 'bg-warning/15 text-warning border-warning/20',
    published: 'bg-success/15 text-success border-success/20',
  };
  return colors[status] || 'bg-muted text-muted-foreground border-muted';
}

/**
 * Retorna o label
 * @param {string} status - Status da sessão (ex: "scheduled", "completed", "canceled")
 * @returns {string} Label do status
 */
export function getStatusLabel(status) {
  const labels = {
    scheduled: 'Agendada',
    completed: 'Concluída',
    canceled: 'Cancelada',
    missed: 'Falta',
    active: 'Ativo',
    archived: 'Arquivado',
    draft: 'Rascunho',
    published: 'Publicado',
  };
  return labels[status] || status;
}

/**
 * Retorna o label em português para o sexo
 * @param {string} sex - Sexo do cliente (ex: "male", "female")
 * @return {string} Label do sexo (ex: "Masculino", "Feminino")
 */
export function getSexLabel(sex) {
  const labels = {
    male: 'Masculino',
    female: 'Feminino',
    other: 'Outro',
    unknown: 'Desconhecido',
  };
  return labels[sex] || '-';
}
