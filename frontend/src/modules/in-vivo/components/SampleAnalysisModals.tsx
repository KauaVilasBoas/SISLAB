import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { formatAmount } from '@/modules/in-vivo/presentation';
import {
  useAnalyseSample,
  useRecordAnalysisResult,
} from '@/modules/in-vivo/api/biobank.queries';

/** Run-an-analysis form: consumes an aliquot in the sample's unit (bounded by the remaining balance). */
export function AnalyseSampleModal({
  sampleId,
  unit,
  remaining,
  onClose,
}: {
  sampleId: string;
  unit: string;
  remaining: number;
  onClose: () => void;
}) {
  const toast = useToast();
  const analyse = useAnalyseSample(sampleId);
  const [name, setName] = useState('');
  const [quantity, setQuantity] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await analyse.mutateAsync({
        name: name.trim(),
        consumedQuantity: Number(quantity),
        unit,
      });
      toast('success', 'Análise registrada.');
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar a análise.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova análise"
      description={`Saldo disponível: ${formatAmount(remaining, unit)}.`}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={analyse.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="analyse-sample-form" disabled={analyse.isPending}>
            {analyse.isPending && <Loader2 className="size-4 animate-spin" />}
            Registrar análise
          </Button>
        </>
      }
    >
      <form
        id="analyse-sample-form"
        className="space-y-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="space-y-1.5">
          <Label htmlFor="analysis-name">Análise</Label>
          <Input
            id="analysis-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Ex.: ELISA TNF-α"
            maxLength={200}
            required
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="analysis-qty">Quantidade consumida ({unit})</Label>
          <Input
            id="analysis-qty"
            type="number"
            min={0}
            max={remaining}
            step="any"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
            required
          />
        </div>
      </form>
    </Modal>
  );
}

/** Record-a-result form: signs off a pending analysis with its free-text result. */
export function RecordResultModal({
  sampleId,
  analysisId,
  analysisName,
  onClose,
}: {
  sampleId: string;
  analysisId: string;
  analysisName: string;
  onClose: () => void;
}) {
  const toast = useToast();
  const record = useRecordAnalysisResult(sampleId, analysisId);
  const [result, setResult] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await record.mutateAsync({ result: result.trim() });
      toast('success', 'Resultado registrado.');
      onClose();
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível registrar o resultado.',
      );
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Registrar resultado"
      description={analysisName}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={record.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="record-result-form" disabled={record.isPending}>
            {record.isPending && <Loader2 className="size-4 animate-spin" />}
            Salvar resultado
          </Button>
        </>
      }
    >
      <form
        id="record-result-form"
        className="space-y-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="space-y-1.5">
          <Label htmlFor="analysis-result">Resultado</Label>
          <textarea
            id="analysis-result"
            value={result}
            onChange={(e) => setResult(e.target.value)}
            placeholder="Ex.: 42.7 pg/mL"
            maxLength={4000}
            rows={3}
            required
            className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
      </form>
    </Modal>
  );
}
