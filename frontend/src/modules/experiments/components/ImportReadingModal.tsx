import { useState, type ChangeEvent, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useImportReading } from '@/modules/experiments/api/experiments.queries';

interface ImportReadingModalProps {
  experimentId: string;
  onClose: () => void;
}

/**
 * Import-plate-reading modal (card [E11] #68). Accepts the canonical <c>well,absorbance</c> CSV either by
 * pasting it or by picking a .csv file (read client-side into the same textarea). The content is posted
 * as-is; the backend parser validates every line and rejects malformed input.
 */
export function ImportReadingModal({ experimentId, onClose }: ImportReadingModalProps) {
  const toast = useToast();
  const importReading = useImportReading(experimentId);
  const [csv, setCsv] = useState('');

  function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => setCsv(String(reader.result ?? ''));
    reader.readAsText(file);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await importReading.mutateAsync(csv);
      toast('success', 'Leitura importada.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível importar a leitura.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Importar leitura da placa"
      description="Cole o CSV do leitor (formato well,absorbance — ex.: A1,0.452) ou selecione um arquivo .csv."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={importReading.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="import-reading-form"
            disabled={importReading.isPending || csv.trim() === ''}
          >
            {importReading.isPending && <Loader2 className="size-4 animate-spin" />}
            Importar
          </Button>
        </>
      }
    >
      <form id="import-reading-form" className="space-y-4" onSubmit={handleSubmit} noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="csv-file">Arquivo (opcional)</Label>
          <input
            id="csv-file"
            type="file"
            accept=".csv,text/csv"
            onChange={handleFile}
            className="block w-full text-sm text-muted-foreground file:mr-3 file:rounded-md file:border file:border-input file:bg-transparent file:px-3 file:py-1.5 file:text-sm file:font-medium"
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="csv-content">Conteúdo CSV</Label>
          <textarea
            id="csv-content"
            value={csv}
            onChange={(e) => setCsv(e.target.value)}
            rows={10}
            placeholder={'A1,0.452\nB1,1.03\nC1,0.55'}
            className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 font-mono text-xs shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
      </form>
    </Modal>
  );
}
