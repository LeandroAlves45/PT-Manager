import {
  Apple,
  BarChart3,
  Boxes,
  CalendarDays,
  ClipboardCheck,
  CreditCard,
  Dumbbell,
  LayoutDashboard,
  Library,
  Palette,
  Pill,
  ShieldCheck,
  User,
  Users,
  UtensilsCrossed,
  type LucideIcon,
} from 'lucide-react';

import type { AppRole } from '@/shared/api/session';

/**
 * Navegação por papel.
 *
 * Os ecrãs em si chegam nas fases 6D a 6F; a 6C fixa a estrutura, as rotas e os ícones
 * para que a navegação exista desde o primeiro dia e não seja reinventada por fase.
 */

export interface NavigationItem {
  readonly label: string;
  readonly route: string;
  readonly icon: LucideIcon;
  readonly exact?: boolean;
}

export interface NavigationGroup {
  readonly label: string;
  readonly items: readonly NavigationItem[];
}

const TRAINER_NAVIGATION: readonly NavigationGroup[] = [
  {
    label: 'Principal',
    items: [
      { label: 'Painel', route: '/trainer', icon: LayoutDashboard, exact: true },
      { label: 'Clientes', route: '/trainer/clients', icon: Users },
      { label: 'Sessões e packs', route: '/trainer/sessions', icon: CalendarDays },
      { label: 'Check-ins', route: '/trainer/check-ins', icon: ClipboardCheck },
    ],
  },
  {
    label: 'Prescrição',
    items: [
      { label: 'Planos de treino', route: '/trainer/training-plans', icon: Dumbbell },
      { label: 'Planos alimentares', route: '/trainer/meal-plans', icon: UtensilsCrossed },
      { label: 'Biblioteca', route: '/trainer/library', icon: Library },
    ],
  },
  {
    label: 'Definições',
    items: [
      { label: 'Marca própria', route: '/trainer/settings', icon: Palette },
      { label: 'Subscrição', route: '/trainer/billing', icon: CreditCard },
    ],
  },
];

const ADMIN_NAVIGATION: readonly NavigationGroup[] = [
  {
    label: 'Plataforma',
    items: [
      { label: 'Visão geral', route: '/admin', icon: BarChart3, exact: true },
      { label: 'Moderação', route: '/admin/moderation', icon: ShieldCheck },
    ],
  },
  {
    label: 'Catálogos',
    items: [
      { label: 'Alimentos', route: '/admin/catalog/foods', icon: Apple },
      { label: 'Exercícios', route: '/admin/catalog/exercises', icon: Dumbbell },
      { label: 'Suplementos', route: '/admin/catalog/supplements', icon: Pill },
    ],
  },
];

/**
 * Portal do cliente: barra inferior de quatro itens.
 *
 * Os check-ins pendentes aparecem como cartão na home e têm rota própria
 * (`/portal/check-ins`), mas não ocupam um item da barra.
 */
const PORTAL_NAVIGATION: readonly NavigationGroup[] = [
  {
    label: 'Portal',
    items: [
      { label: 'Treino', route: '/portal/today', icon: Dumbbell },
      { label: 'Nutrição', route: '/portal/nutrition', icon: UtensilsCrossed },
      { label: 'Suplementos', route: '/portal/supplements', icon: Boxes },
      { label: 'Perfil', route: '/portal/profile', icon: User },
    ],
  },
];

/** Devolve os grupos de navegação do papel indicado. */
export function navigationFor(role: AppRole): readonly NavigationGroup[] {
  switch (role) {
    case 'superuser':
      return ADMIN_NAVIGATION;
    case 'trainer':
      return TRAINER_NAVIGATION;
    case 'client':
      return PORTAL_NAVIGATION;
  }
}

/** Etiqueta legível do papel, para o menu de perfil. */
export function roleLabel(role: AppRole): string {
  switch (role) {
    case 'superuser':
      return 'Administrador';
    case 'trainer':
      return 'Personal Trainer';
    case 'client':
      return 'Cliente';
  }
}
