import type { ReactNode } from 'react';
import { Loader2, PackageX, ShieldCheck } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import type { PagedResult } from '@/shared/types/api';
import type { StockItemListItem } from '@/modules/inventory/types';
import {
  containerStateLabel,
  expiryDotPresentation,
  expiryStatusPresentation,
  formatExpiry,
  formatQuantity,
} from '@/modules/inventory/components/stock-presentation';

interface StockItemsTableProps {
  query: {
    data?: PagedResult<StockItemListItem>;
    isLoading: boolean;
    isError: boolean;
  };
  onSelect: (item: StockItemListItem) => void;
}

const COLUMNS = [
  'Item',
  'Categoria',
  'Marca',
  'Lote',
  'Quantidade',
  'Validade',
  'Estado',
  'Local',
] as const;

/**
 * Presentational stock-item table (card [E7] #46). Renders the paginated read rows and delegates
 * loading/error/empty to standardized states. A row click hands the item up so the page opens the
 * detail sheet — the table itself is stateless.
 */
export function StockItemsTable({ query, onSelect }: StockItemsTableProps) {
  if (query.isLoading) {
    return (
      <StateCard>
        <Loader2 className="size-4 animate-spin" />
        Carregando itens…
      </StateCard>
    );
  }

  if (query.isError) {
    return (
      <StateCard tone="error">Não foi possível carregar os itens de estoque.</StateCard>
    );
  }

  const items = query.data?.items ?? [];

  if (items.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
          <PackageX className="size-8 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Nenhum item encontrado. Ajuste os filtros ou cadastre o primeiro item.
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
              const expiry = expiryStatusPresentation(item.expiryStatus);
              const dot = expiryDotPresentation(item.expiryStatus);
              return (
                <tr
                  key={item.id}
                  onClick={() => onSelect(item)}
                  className="cursor-pointer border-b transition-colors last:border-0 hover:bg-accent/50"
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5 font-medium">
                      {item.name}
                      {item.isControlled ? (
                        <ShieldCheck
                          className="size-3.5 text-muted-foreground"
                          aria-label="Item controlado"
                        />
                      ) : null}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{item.category}</td>
                  <td className="px-4 py-3 text-muted-foreground">{item.brand ?? '—'}</td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {item.lotCode ?? '—'}
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    <span
                      className={
                        item.isBelowMinimum ? 'font-medium text-destructive' : ''
                      }
                    >
                      {formatQuantity(item.quantity, item.unit)}
                    </span>
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">
                    <div className="flex items-center gap-2">
                      <span
                        className={`inline-block size-2 shrink-0 rounded-full ${dot.className}`}
                        title={dot.title}
                        aria-label={dot.title}
                      />
                      <span>{formatExpiry(item.expiryYear, item.expiryMonth)}</span>
                      <Badge variant={expiry.variant}>{expiry.label}</Badge>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {containerStateLabel(item.containerState)}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {item.storageLocationName ?? '—'}
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
