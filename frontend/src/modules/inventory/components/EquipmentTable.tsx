import type { ReactNode } from 'react';
import { AlertTriangle, Loader2, Monitor, PackageX } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import type { PagedResult } from '@/shared/types/api';
import type { EquipmentListItem } from '@/modules/inventory/equipment.types';
import {
  calibrationStatusPresentation,
  equipmentStatusPresentation,
  formatCalibrationMonth,
  isCalibrationOverdue,
} from '@/modules/inventory/components/equipment-presentation';

interface EquipmentTableProps {
  query: {
    data?: PagedResult<EquipmentListItem>;
    isLoading: boolean;
    isError: boolean;
  };
  onSelect: (item: EquipmentListItem) => void;
}

const COLUMNS = [
  'Equipamento',
  'Marca / Modelo',
  'Tombamento',
  'Local',
  'Status',
  'Últ. calibração',
] as const;

/**
 * Presentational equipment table (card [E7] #48). Renders the paginated read rows with the prototype's
 * columns — monitor icon + name, asset tag (mono), status pill and a "Últ. calibração" cell that
 * surfaces an amber warning icon when the derived calibration status is overdue. A row click hands the
 * item up so the page opens the detail sheet; the table itself is stateless.
 */
export function EquipmentTable({ query, onSelect }: EquipmentTableProps) {
  if (query.isLoading) {
    return (
      <StateCard>
        <Loader2 className="size-4 animate-spin" />
        Carregando equipamentos…
      </StateCard>
    );
  }

  if (query.isError) {
    return <StateCard tone="error">Não foi possível carregar os equipamentos.</StateCard>;
  }

  const items = query.data?.items ?? [];

  if (items.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
          <PackageX className="size-8 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Nenhum equipamento encontrado. Ajuste os filtros ou cadastre o primeiro.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <div className="overflow-x-auto scrollbar-thin">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {COLUMNS.map((col) => (
                <th key={col} className="px-4 py-3 whitespace-nowrap">
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const status = equipmentStatusPresentation(item.status);
              const calibration = calibrationStatusPresentation(item.calibrationStatus);
              const overdue = isCalibrationOverdue(item.calibrationStatus);
              return (
                <tr
                  key={item.id}
                  onClick={() => onSelect(item)}
                  className="cursor-pointer border-b transition-colors last:border-0 hover:bg-accent/50"
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2 font-medium">
                      <Monitor className="size-4 shrink-0 text-muted-foreground" />
                      {item.name}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {[item.manufacturer, item.model].filter(Boolean).join(' / ') || '—'}
                  </td>
                  <td className="px-4 py-3 font-mono text-muted-foreground">
                    {item.assetTag}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {item.storageLocationName ?? '—'}
                  </td>
                  <td className="px-4 py-3">
                    <span className={status.className}>{status.label}</span>
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    <div className="flex items-center gap-2">
                      {overdue ? (
                        <AlertTriangle
                          className="size-3.5 text-amber-600 dark:text-amber-400"
                          aria-label="Calibração atrasada"
                        />
                      ) : null}
                      <span>{formatCalibrationMonth(item.nextCalibrationDate)}</span>
                      <span className={calibration.className}>{calibration.label}</span>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function StateCard({
  children,
  tone = 'muted',
}: {
  children: ReactNode;
  tone?: 'muted' | 'error';
}) {
  return (
    <Card>
      <CardContent
        className={
          tone === 'error'
            ? 'py-16 text-center text-sm text-destructive'
            : 'flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground'
        }
      >
        {children}
      </CardContent>
    </Card>
  );
}
