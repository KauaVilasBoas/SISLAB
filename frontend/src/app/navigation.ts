import {
  LayoutDashboard,
  Boxes,
  ShieldCheck,
  Monitor,
  Handshake,
  Users,
  Settings2,
  ScrollText,
  CalendarDays,
  Repeat,
  Presentation,
  FlaskConical,
  FlaskRound,
  QrCode,
  Wallet,
  Snowflake,
  ListChecks,
  type LucideIcon,
} from 'lucide-react';
import { Permissions } from '@/modules/auth/permissions';

export interface NavItem {
  /** Route path (also the React Router path). */
  path: string;
  label: string;
  /** Short description shown under the label in the sidebar and as the Topbar subtitle. */
  description: string;
  icon: LucideIcon;
  /** Future module without a card yet — rendered but non-navigable (card [E7] #43 scope note). */
  disabled?: boolean;
  /**
   * Read permission codes that gate this entry (card [E7] #110). When present the item only renders if the
   * user holds AT LEAST ONE of them in the active company — used for admin screens whose backend read
   * endpoints are [RequirePermission]-gated. Absent ⇒ visible to any authenticated user (the item's read
   * endpoints are only [Authorize], so inventing a gate here would hide a screen the server would serve).
   */
  permissionAny?: readonly string[];
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}

/**
 * Grouped primary navigation matching the prototype shell (card [E7] #43).
 *
 * Only "Geral" (Dashboard) and "Recursos" are active today. "Agenda" and "Experimentos" are future
 * modules with no cards yet: they appear (so the structure is visible) but are disabled until
 * prioritized. "Administração" groups the already-built admin screens. Notifications live in the
 * Topbar bell, not here.
 */
export const navGroups: NavGroup[] = [
  {
    title: 'Geral',
    items: [
      {
        path: '/',
        label: 'Dashboard',
        description: 'Visão geral',
        icon: LayoutDashboard,
      },
    ],
  },
  {
    title: 'Agenda',
    items: [
      {
        path: '/agenda/rooms',
        label: 'Agendamento',
        description: 'Reservas de salas',
        icon: CalendarDays,
      },
      {
        path: '/agenda/bioterium',
        label: 'Troca do biotério',
        description: 'Escala seg/qui',
        icon: Repeat,
      },
      {
        path: '/agenda/presentations',
        label: 'Apresentações',
        description: 'Seminários LAFTE e DOL',
        icon: Presentation,
      },
      {
        path: '/agenda/calendar',
        label: 'Calendário',
        description: 'Visão unificada da semana',
        icon: CalendarDays,
      },
    ],
  },
  {
    title: 'Experimentos',
    items: [
      {
        // In vitro cell-viability slice (card [E11] #68). Visible to every member for now — the list/detail
        // read endpoints are only [Authorize]-gated, so no permissionAny here.
        path: '/experiments',
        label: 'In vitro',
        description: 'Viabilidade celular',
        icon: FlaskRound,
      },
      {
        // In vivo experimental design (card [E11] #73). Visible to every member — the list/detail read
        // endpoints are only [Authorize]-gated; the write actions are permission-gated on the backend.
        path: '/experiments/in-vivo/projects',
        label: 'In vivo',
        description: 'Projetos e delineamento',
        icon: FlaskConical,
      },
      {
        path: '/experiments/in-vivo/biobank',
        label: 'Biobanco',
        description: 'Amostras e análises',
        icon: Snowflake,
      },
      {
        path: '/experiments/in-vivo/pendencies',
        label: 'Pendências',
        description: 'Trabalho em aberto',
        icon: ListChecks,
      },
    ],
  },
  {
    title: 'Recursos',
    items: [
      {
        path: '/inventory',
        label: 'Estoque',
        description: 'Itens e movimentações',
        icon: Boxes,
      },
      {
        path: '/controlled',
        label: 'Controlados',
        description: 'Saldo por frasco',
        icon: ShieldCheck,
      },
      {
        path: '/labels',
        label: 'Etiquetas QR',
        description: 'Gerar e imprimir QR',
        icon: QrCode,
      },
      {
        path: '/equipment',
        label: 'Equipamentos',
        description: 'Calibração e manutenção',
        icon: Monitor,
      },
      {
        path: '/partners',
        label: 'Parceiros',
        description: 'Instituições e amostras',
        icon: Handshake,
      },
      {
        path: '/inventory/cost-report',
        label: 'Relatório de custos',
        description: 'Gasto por mês e experimento',
        icon: Wallet,
        // Cost is gestão-sensitive: both report endpoints are Inventory.Cost.Read-gated, so hide the entry
        // for users without the capability (the page itself renders "acesso restrito" as a fallback).
        permissionAny: [Permissions.inventory.costRead],
      },
    ],
  },
  {
    title: 'Administração',
    items: [
      {
        path: '/members',
        label: 'Membros & Perfis',
        description: 'Acesso e permissões',
        icon: Users,
        // Two tabs (Members / Profiles); the screen is reachable if the user can list either. Both backend
        // read endpoints are [RequirePermission]-gated, so the whole entry is hidden without either code.
        permissionAny: [
          Permissions.members.listEnriched,
          Permissions.profiles.listProfiles,
        ],
      },
      {
        path: '/configuration',
        label: 'Configurações',
        description: 'Catálogos do laboratório',
        icon: Settings2,
      },
      {
        path: '/audit',
        label: 'Auditoria',
        description: 'Trilha de ações',
        icon: ScrollText,
      },
    ],
  },
];

/** Flat list of navigable items — used to resolve the Topbar title/subtitle from the route. */
export const navItems: NavItem[] = navGroups.flatMap((group) => group.items);
