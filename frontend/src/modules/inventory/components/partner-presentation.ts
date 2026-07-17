import type { PartnerType } from '@/modules/inventory/partner.types';

/**
 * Presentation helpers for the partners screen (card [E7] #48): Portuguese labels and the Tailwind
 * badge classes the prototype specifies for the partner type (Fornecedor=blue, Ambos=violet,
 * Parceiro/cliente=green). Kept in one place so the grid and forms stay consistent.
 */

const PILL =
  'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold';

/** Portuguese label + coloured pill classes for a partner's type. */
export function partnerTypePresentation(type: PartnerType): {
  label: string;
  className: string;
} {
  switch (type) {
    case 'Supplier':
      return {
        label: 'Fornecedor',
        className: `${PILL} bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-200`,
      };
    case 'Both':
      return {
        label: 'Ambos',
        className: `${PILL} bg-violet-100 text-violet-800 dark:bg-violet-950 dark:text-violet-200`,
      };
    case 'Client':
    default:
      return {
        label: 'Parceiro',
        className: `${PILL} bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200`,
      };
  }
}

/** The partner types a user may pick from a form/filter, in dropdown order. */
export const PARTNER_TYPES: PartnerType[] = ['Supplier', 'Client', 'Both'];
