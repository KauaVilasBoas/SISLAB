import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, Snowflake } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { useSamples } from '@/modules/in-vivo/api/biobank.queries';
import { formatAmount, formatDate, sampleTypeName } from '@/modules/in-vivo/presentation';

const PAGE_SIZE = 20;

/**
 * Biobank samples list (card [E11] #89). A paginated table of the active company's samples with the derived
 * remaining balance and analysis count; each row navigates to the sample detail (where analyses are run).
 * Collecting a sample happens from an experiment's collection step, so there is no "new" action here.
 */
export function BiobankPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);

  const { data, isLoading, isError } = useSamples({ page, pageSize: PAGE_SIZE });

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Biobanco"
        description="Amostras coletadas dos estudos in vivo — saldo derivado e análises."
      />

      <div className="rounded-lg border bg-card">
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando amostras…
          </div>
        ) : isError ? (
          <p className="p-10 text-center text-sm text-destructive">
            Não foi possível carregar as amostras.
          </p>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center gap-2 p-10 text-center text-sm text-muted-foreground">
            <Snowflake className="size-6 text-muted-foreground/60" />
            Nenhuma amostra ainda. Colete amostras a partir da etapa de coleta de um
            experimento in vivo.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-medium">Código</th>
                <th className="px-4 py-3 font-medium">Tipo</th>
                <th className="px-4 py-3 font-medium">Coletado</th>
                <th className="px-4 py-3 font-medium">Saldo</th>
                <th className="px-4 py-3 font-medium">Análises</th>
                <th className="px-4 py-3 font-medium">Coletado em</th>
              </tr>
            </thead>
            <tbody>
              {items.map((sample) => {
                const depleted = sample.remainingQuantity <= 0;
                return (
                  <tr
                    key={sample.id}
                    onClick={() => navigate(`/experiments/in-vivo/biobank/${sample.id}`)}
                    className="cursor-pointer border-b last:border-0 transition-colors hover:bg-accent/50"
                  >
                    <td className="px-4 py-3 font-medium">{sample.code}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {sampleTypeName(sample.type)}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatAmount(sample.collectedQuantity, sample.unit)}
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant={depleted ? 'outline' : 'default'}>
                        {formatAmount(sample.remainingQuantity, sample.unit)}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {sample.analysisCount}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDate(sample.collectedAtUtc)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Anterior
          </Button>
          <span className="text-sm text-muted-foreground">
            Página {page} de {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Próxima
          </Button>
        </div>
      )}
    </div>
  );
}
