import type { DocumentPath, SiteDocument } from '@/domain/types';

/**
 * Páginas de texto do site: Privacidade, Termos, Suporte e Eliminar conta.
 *
 * O texto é um rascunho técnico, escrito a partir do que o código faz (dados guardados,
 * fornecedores, cookies, eliminação). Não substitui revisão jurídica. Enquanto
 * `legalReviewed` for `false`, as páginas saem com `noindex` (continuam acessíveis por URL,
 * que é o que as lojas de apps e o Google exigem) e não entram no sitemap.
 */

/** Passa a `true` depois da revisão jurídica e de preencher `legalOperator`. */
export const legalReviewed = false;

/** Marca de um campo por preencher. Um teste impede publicar como revisto com ela. */
export const PENDING_FIELD = 'A preencher antes da publicação';

/** Quem é responsável pelo serviço (pessoa singular, sem empresa). */
export const legalOperator = {
  /** Nome completo do responsável, como vai aparecer nas lojas de apps. */
  name: PENDING_FIELD,
  /** Morada ou localidade de contacto (as lojas podem exigir morada pública na UE). */
  address: PENDING_FIELD,
} as const;

/** Data da última revisão de todos os textos. */
const UPDATED_ON = '2026-10-28';

export interface LegalLinks {
  /** Origem da aplicação (`https://app.<dominio>`). */
  appUrl: string;
  contactEmail: string;
}

/**
 * Fornecedores que tratam dados por conta do PT Manager (subcontratantes), com a função de
 * cada um. Lista tirada das integrações do backend; mudar um fornecedor exige mudar aqui.
 */
export const processors: readonly string[] = [
  'Render: alojamento da API.',
  'Vercel: alojamento da aplicação web e deste site.',
  'Neon: base de dados PostgreSQL.',
  'Resend: envio de emails (convites, confirmações, recuperação de palavra-passe, lembretes).',
  'Stripe: pagamento das subscrições dos personal trainers (o PT Manager não guarda dados de cartão).',
  'Cloudinary: fotografias de perfil e logótipos.',
  'Google Cloud Vision: verificação automática das fotografias de perfil antes de serem publicadas.',
  'Cloudflare R2: vídeos de exercícios carregados pelos personal trainers.',
  'Upstash QStash: agendamento das tarefas automáticas (por exemplo, lembretes).',
  'Sentry: registo de erros técnicos, configurado para não recolher dados pessoais.',
  'Grafana Cloud: métricas e registos técnicos de funcionamento.',
  'Google (início de sessão com Google): só quando escolhes entrar com a tua conta Google.',
];

function privacy({ appUrl, contactEmail }: LegalLinks): SiteDocument {
  return {
    path: '/privacidade',
    linkLabel: 'Privacidade',
    title: 'Política de privacidade',
    description:
      'Que dados pessoais o PT Manager trata, para quê, com que fornecedores, durante quanto tempo e como exerces os teus direitos.',
    updatedOn: UPDATED_ON,
    intro: [
      'Esta política explica como o PT Manager trata dados pessoais na aplicação web, nas apps e neste site.',
      `Responsável: ${legalOperator.name} (${legalOperator.address}). Contacto: ${contactEmail}.`,
    ],
    sections: [
      {
        id: 'papeis',
        heading: 'Quem decide sobre os dados',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Para as contas de personal trainer, o PT Manager é o responsável pelo tratamento.',
          },
          {
            kind: 'paragraph',
            text: 'Os dados dos clientes (ficha, avaliações, planos, check-ins) são introduzidos e geridos pelo personal trainer, que é o responsável por esses dados. O PT Manager trata-os por conta dele, como subcontratante. Se és cliente, os pedidos sobre os teus dados podem ser feitos ao teu personal trainer ou a nós, e encaminhamos o que for dele.',
          },
        ],
      },
      {
        id: 'dados',
        heading: 'Que dados tratamos',
        blocks: [
          {
            kind: 'list',
            items: [
              'Conta: nome, email, palavra-passe (guardada só como hash) e, se usares o Google, o identificador da tua conta Google.',
              'Ficha do cliente: nome, email e telefone de contacto, data de nascimento, sexo, objetivo, contacto de emergência, notas e fotografia de perfil.',
              'Dados de saúde e físicos: avaliação inicial (incluindo condições médicas), peso, medidas, check-ins, registos de treino, planos alimentares e suplementação.',
              'Atividade na plataforma: sessões marcadas, treinos concluídos, notificações enviadas.',
              'Faturação dos personal trainers: plano escolhido e estado da subscrição. Os dados de pagamento ficam no Stripe.',
              'Dados técnicos: registos de acesso e de erro (endereço IP, data e hora, pedido), necessários à segurança e ao funcionamento.',
            ],
          },
        ],
      },
      {
        id: 'finalidades',
        heading: 'Para que usamos os dados',
        blocks: [
          {
            kind: 'list',
            items: [
              'Prestar o serviço: gerir clientes, planos, sessões e check-ins (execução do contrato).',
              'Enviar os emails do serviço: convites, confirmações, recuperação de palavra-passe e lembretes.',
              'Cobrar as subscrições dos personal trainers (execução do contrato e obrigações legais).',
              'Proteger o serviço: limitar tentativas de acesso, detetar abusos e corrigir erros (interesse legítimo).',
            ],
          },
          {
            kind: 'paragraph',
            text: 'Os dados de saúde só são tratados para o acompanhamento entre o cliente e o seu personal trainer. Não vendemos dados, não os usamos para publicidade e não fazemos perfis para fins comerciais.',
          },
        ],
      },
      {
        id: 'fornecedores',
        heading: 'Fornecedores (subcontratantes)',
        blocks: [
          { kind: 'list', items: processors },
          {
            kind: 'paragraph',
            text: 'Quando um fornecedor trata dados fora do Espaço Económico Europeu, a transferência apoia-se nas garantias previstas no RGPD, como as cláusulas contratuais-tipo da Comissão Europeia.',
          },
        ],
      },
      {
        id: 'cookies',
        heading: 'Cookies e armazenamento no dispositivo',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Este site não usa cookies nem ferramentas de análise.',
          },
          {
            kind: 'paragraph',
            text: 'A aplicação usa apenas o cookie estritamente necessário para manter a sessão iniciada (até 30 dias, renovado enquanto usas a aplicação) e guarda no navegador a preferência de tema claro ou escuro. Não há cookies de publicidade nem de análise.',
          },
        ],
      },
      {
        id: 'conservacao',
        heading: 'Durante quanto tempo guardamos os dados',
        blocks: [
          {
            kind: 'list',
            items: [
              'Enquanto a conta estiver ativa.',
              'Quando um cliente elimina a conta, o acesso e os dados que o identificam são apagados de imediato; o histórico de treino e avaliações fica com o personal trainer, sem identificar a pessoa.',
              'As cópias de segurança do fornecedor da base de dados são substituídas automaticamente num prazo curto.',
              'Os registos técnicos são guardados pelo tempo necessário à segurança e à resolução de erros.',
            ],
          },
        ],
      },
      {
        id: 'direitos',
        heading: 'Os teus direitos',
        blocks: [
          {
            kind: 'paragraph',
            text: `Podes pedir acesso, retificação, apagamento, limitação, portabilidade e opor-te ao tratamento, escrevendo para ${contactEmail}. Respondemos no prazo de um mês.`,
          },
          {
            kind: 'paragraph',
            text: 'Podes também apresentar reclamação à Comissão Nacional de Proteção de Dados (CNPD).',
          },
          {
            kind: 'links',
            items: [
              { label: 'Como eliminar a tua conta', href: '/eliminar-conta' },
              { label: 'Abrir a aplicação', href: `${appUrl}/entrar` },
            ],
          },
        ],
      },
      {
        id: 'alteracoes',
        heading: 'Alterações a esta política',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Se mudarmos a forma como tratamos os dados, atualizamos esta página e a data no topo. Alterações importantes são comunicadas por email.',
          },
        ],
      },
    ],
  };
}

function terms({ contactEmail }: LegalLinks): SiteDocument {
  return {
    path: '/termos',
    linkLabel: 'Termos',
    title: 'Termos de utilização',
    description:
      'As regras de utilização do PT Manager: contas, planos e pagamentos, responsabilidades do personal trainer e limites do serviço.',
    updatedOn: UPDATED_ON,
    intro: [
      `Ao criar uma conta ou usar o PT Manager aceitas estes termos. Prestador: ${legalOperator.name}. Contacto: ${contactEmail}.`,
    ],
    sections: [
      {
        id: 'servico',
        heading: 'O serviço',
        blocks: [
          {
            kind: 'paragraph',
            text: 'O PT Manager é uma plataforma de gestão para personal trainers e nutricionistas: clientes, planos de treino, nutrição, suplementação, sessões e check-ins, com um portal para os clientes.',
          },
        ],
      },
      {
        id: 'contas',
        heading: 'Contas',
        blocks: [
          {
            kind: 'list',
            items: [
              'Os personal trainers criam a própria conta. Os clientes entram por convite do seu personal trainer.',
              'És responsável por manter a palavra-passe segura e pelos acessos feitos com a tua conta.',
              'Os dados da conta têm de ser verdadeiros e estar atualizados.',
            ],
          },
        ],
      },
      {
        id: 'planos-e-pagamentos',
        heading: 'Planos e pagamentos',
        blocks: [
          {
            kind: 'list',
            items: [
              'O plano gratuito tem um limite de clientes ativos. Os planos pagos são mensais e cobrados pelo Stripe.',
              'Só os personal trainers pagam. Os clientes não pagam nada ao PT Manager.',
              'Podes mudar de plano ou cancelar a subscrição a qualquer momento na área Subscrição da aplicação; as condições do cancelamento são mostradas antes de confirmares.',
            ],
          },
        ],
      },
      {
        id: 'responsabilidades',
        heading: 'Responsabilidades do personal trainer',
        blocks: [
          {
            kind: 'list',
            items: [
              'Tens de ter base legal para tratar os dados dos teus clientes, incluindo os dados de saúde, e informá-los sobre esse tratamento.',
              'Os planos de treino e de nutrição são da tua responsabilidade profissional. O PT Manager não presta aconselhamento médico.',
              'Não podes usar o serviço para fins ilegais, enviar conteúdo ofensivo nem tentar aceder a dados de outras contas.',
            ],
          },
        ],
      },
      {
        id: 'disponibilidade',
        heading: 'Disponibilidade e responsabilidade',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Fazemos o possível para manter o serviço disponível e seguro, mas podem existir interrupções para manutenção ou por falhas de terceiros. Na medida permitida por lei, não respondemos por danos indiretos resultantes dessas interrupções.',
          },
        ],
      },
      {
        id: 'fim',
        heading: 'Fim da conta',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Podes deixar de usar o serviço a qualquer momento. Podemos suspender contas que violem estes termos, avisando sempre que possível.',
          },
          { kind: 'links', items: [{ label: 'Como eliminar a conta', href: '/eliminar-conta' }] },
        ],
      },
      {
        id: 'lei',
        heading: 'Lei aplicável',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Estes termos regem-se pela lei portuguesa, sem prejuízo dos direitos que a lei do teu país de residência te garanta enquanto consumidor.',
          },
        ],
      },
    ],
  };
}

function support({ appUrl, contactEmail }: LegalLinks): SiteDocument {
  return {
    path: '/suporte',
    linkLabel: 'Suporte',
    title: 'Suporte',
    description:
      'Ajuda com a conta PT Manager: entrar, recuperar a palavra-passe, convites de clientes, subscrições e contacto por email.',
    updatedOn: UPDATED_ON,
    intro: [`Precisas de ajuda? Escreve para ${contactEmail}. Respondemos em dias úteis.`],
    sections: [
      {
        id: 'entrar',
        heading: 'Entrar e palavra-passe',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Se não te lembras da palavra-passe, pede uma nova na página de recuperação. Recebes um email com um link válido por pouco tempo.',
          },
          {
            kind: 'links',
            items: [
              { label: 'Entrar', href: `${appUrl}/entrar` },
              { label: 'Recuperar a palavra-passe', href: `${appUrl}/recuperar-password` },
            ],
          },
        ],
      },
      {
        id: 'convites',
        heading: 'Sou cliente e recebi um convite',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Abre o link do email de convite e escolhe a tua palavra-passe. Se o convite expirou, pede ao teu personal trainer para o enviar de novo.',
          },
        ],
      },
      {
        id: 'subscricao',
        heading: 'Subscrição (personal trainers)',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Mudas de plano, atualizas o cartão ou cancelas a subscrição na área Subscrição da aplicação.',
          },
        ],
      },
      {
        id: 'falar-connosco',
        heading: 'Contacto',
        blocks: [
          {
            kind: 'links',
            items: [
              { label: `Email: ${contactEmail}`, href: `mailto:${contactEmail}` },
              { label: 'Eliminar a conta', href: '/eliminar-conta' },
              { label: 'Política de privacidade', href: '/privacidade' },
            ],
          },
        ],
      },
    ],
  };
}

function accountDeletion({ appUrl, contactEmail }: LegalLinks): SiteDocument {
  return {
    path: '/eliminar-conta',
    linkLabel: 'Eliminar conta',
    title: 'Eliminar a conta',
    description:
      'Como eliminar a tua conta PT Manager na aplicação ou por email, o que é apagado de imediato e o que fica com o personal trainer.',
    updatedOn: UPDATED_ON,
    intro: ['Podes eliminar a tua conta a qualquer momento, na aplicação ou por email.'],
    sections: [
      {
        id: 'cliente',
        heading: 'Se és cliente',
        blocks: [
          {
            kind: 'list',
            items: [
              'Entra na aplicação ou na app PT Manager.',
              'Abre "Conta e segurança" no menu do teu perfil.',
              'Escolhe "Eliminar conta", confirma com a tua palavra-passe e escreve ELIMINAR.',
            ],
          },
          {
            kind: 'paragraph',
            text: 'A sessão termina em todos os dispositivos e deixas de poder entrar. A aplicação confirma a eliminação no ecrã.',
          },
          { kind: 'links', items: [{ label: 'Entrar na aplicação', href: `${appUrl}/entrar` }] },
        ],
      },
      {
        id: 'personal-trainer',
        heading: 'Se és personal trainer',
        blocks: [
          {
            kind: 'paragraph',
            text: `Cancela primeiro a subscrição na área Subscrição e depois escreve para ${contactEmail} a partir do email da conta, a pedir a eliminação. Tratamos o pedido no prazo de um mês.`,
          },
        ],
      },
      {
        id: 'sem-acesso',
        heading: 'Sem acesso à conta',
        blocks: [
          {
            kind: 'paragraph',
            text: `Se já não consegues entrar, escreve para ${contactEmail} a partir do email associado à conta. Podemos pedir uma confirmação de identidade antes de eliminar.`,
          },
          {
            kind: 'links',
            items: [
              { label: `Pedir por email`, href: `mailto:${contactEmail}?subject=Eliminar%20conta` },
            ],
          },
        ],
      },
      {
        id: 'o-que-acontece',
        heading: 'O que é apagado e o que fica',
        blocks: [
          {
            kind: 'list',
            items: [
              'Apagado de imediato: acesso à conta, nome, email, telefone, data de nascimento, contacto de emergência, notas, condições médicas, objetivos escritos na avaliação, fotografia de perfil e ligação à conta Google.',
              'Fica com o personal trainer, sem te identificar: histórico de treinos, medidas e avaliações, porque fazem parte do registo profissional dele.',
              'O email fica livre: podes voltar a ser convidado mais tarde com o mesmo endereço.',
            ],
          },
        ],
      },
    ],
  };
}

/** Todas as páginas de texto, pela ordem do rodapé. */
export function buildSiteDocuments(links: LegalLinks): readonly SiteDocument[] {
  return [privacy(links), terms(links), support(links), accountDeletion(links)];
}

/** Página de texto de uma rota (ou `undefined` se a rota não for de texto). */
export function findSiteDocument(
  documents: readonly SiteDocument[],
  path: string
): SiteDocument | undefined {
  return documents.find((document) => document.path === (path as DocumentPath));
}
