import {
  LayoutDashboard,
  Boxes,
  ShieldCheck,
  Monitor,
  Handshake,
  Users,
  Settings2,
  ScrollText,
  CalendarRange,
  FlaskConical,
  FlaskRound,
  Beaker,
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
   * Reserved for the Premium edition. The item stays clickable but renders in a "locked" style (mini
   * padlock + "Premium" chip) and its route leads to the immersive PremiumModuleGate showcase rather
   * than the real screen. The Topbar still resolves the title/subtitle from this entry.
   */
  premium?: boolean;
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
 * Ordered by how often a group is opened, not by module number: "Recursos" (stock, equipment, labels)
 * is daily bench work, so it sits above "Experimentos", whose entries are mostly Premium and lead to
 * the showcase rather than a working screen. "Administração" groups the admin screens. Notifications
 * live in the Topbar bell, not here.
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
        path: '/agenda/schedule',
        label: 'Calendário',
        description: 'Dia, semana, mês e salas',
        icon: CalendarRange,
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
    title: 'Experimentos',
    items: [
      {
        // In vitro cell-viability slice (card [E11] #68) — reserved for the Premium edition. Clicking leads
        // to the immersive showcase (PremiumModuleGate), not the real screen.
        path: '/experiments',
        label: 'In vitro',
        description: 'Viabilidade celular',
        icon: FlaskRound,
        premium: true,
      },
      {
        // In vivo experimental design (card [E11] #73) — Premium. Leads to the showcase.
        path: '/experiments/in-vivo/projects',
        label: 'In vivo',
        description: 'Projetos e delineamento',
        icon: FlaskConical,
        premium: true,
      },
      {
        // Biobank (card [E11] #89) — Premium. Leads to the showcase.
        path: '/experiments/in-vivo/biobank',
        label: 'Biobanco',
        description: 'Amostras e análises',
        icon: Snowflake,
        premium: true,
      },
      {
        // Pendencies panel (card [E11] #90) — Premium. Leads to the showcase.
        path: '/experiments/in-vivo/pendencies',
        label: 'Pendências',
        description: 'Trabalho em aberto',
        icon: ListChecks,
        premium: true,
      },
      {
        // Serial-dilution calculator (SISLAB-05). Stateless compute — free and fully navigable (the GET is only
        // [Authorize]-gated), so no permissionAny and no premium flag here.
        path: '/experiments/dilution',
        label: 'Diluição seriada',
        description: 'Calculadora de placa-mãe',
        icon: Beaker,
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
        // Audit trail (append-only action log) — reserved for the Premium edition. Clicking leads to the
        // immersive showcase (PremiumModuleGate), not the real screen.
        path: '/audit',
        label: 'Auditoria',
        description: 'Trilha de ações',
        icon: ScrollText,
        premium: true,
      },
    ],
  },
];

/** Flat list of navigable items — used to resolve the Topbar title/subtitle from the route. */
export const navItems: NavItem[] = navGroups.flatMap((group) => group.items);
