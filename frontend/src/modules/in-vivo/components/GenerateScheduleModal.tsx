import { useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { CalendarClock, Loader2, Plus, X } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { Badge } from '@/shared/components/ui/badge';
import { MultiSelect } from '@/shared/components/ui/multi-select';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import { useExperimentalModel } from '@/modules/configuration/api/configuration.queries';
import { useMembers } from '@/modules/identity/api/identity.queries';
import { useGenerateSchedule } from '@/modules/in-vivo/api/scheduling.queries';
import type { BatchDetail } from '@/modules/in-vivo/types';

/** Default reminder lead time: 1 day (1440 minutes) before the activity — the "véspera" reminder. */
const DEFAULT_REMINDER_MINUTES = 1440;

/** Today as an ISO `YYYY-MM-DD` string, a sensible default for day 0 (first induction). */
function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

interface GenerateScheduleModalProps {
  /** The experiment the schedule links to — here the batch (leva) whose bound model drives the cadence. */
  experimentId: string;
  batch: BatchDetail;
  onClose: () => void;
}

/**
 * Generates an experiment's schedule from its bound experimental model (SISLAB-10). The batch (leva) already
 * carries the model, so the model is not re-picked here: we read `model.timepoints` to render exactly one day
 * offset per timepoint (in the model's order — mirroring the backend's length check to avoid a 422), and the
 * induction cadence itself comes from the model on the server. The caller edits the start date, the treatment
 * day offsets (an editable list), the roster of responsibles (a rotation with a configurable days-per-shift —
 * two people at one day/shift reproduce the spreadsheet's day-on/day-off alternation) and the véspera reminder.
 *
 * On success it reports how many Agenda entries were created and links straight to the calendar filtered to this
 * run (`/agenda/schedule?experimentId=<batchId>`), since the created entries carry the batch id by value.
 */
export function GenerateScheduleModal({ experimentId, batch, onClose }: GenerateScheduleModalProps) {
  const toast = useToast();
  const model = useExperimentalModel(batch.experimentalModelId);
  const members = useMembers();
  const generate = useGenerateSchedule(experimentId);

  const [startDate, setStartDate] = useState(todayIso());
  const [treatmentOffsets, setTreatmentOffsets] = useState<string[]>(['']);
  // One entry per model timepoint, in order — sized/reset from the model once it loads.
  const [timepointOffsets, setTimepointOffsets] = useState<string[]>([]);
  const [responsibles, setResponsibles] = useState<string[]>([]);
  const [daysPerShift, setDaysPerShift] = useState('1');
  const [reminderEnabled, setReminderEnabled] = useState(true);
  const [reminderMinutes, setReminderMinutes] = useState(String(DEFAULT_REMINDER_MINUTES));

  // Stable reference for the model's timepoints so the derived arrays below don't recompute every render.
  const timepoints = useMemo(() => model.data?.timepoints ?? [], [model.data?.timepoints]);

  // Keep the per-timepoint offsets array in lockstep with the model's timepoint count without an effect: derive
  // the rendered value, padding with blanks, so the inputs always match the model even while it (re)loads.
  const renderedTimepointOffsets = useMemo(
    () => timepoints.map((_, index) => timepointOffsets[index] ?? ''),
    [timepoints, timepointOffsets],
  );

  const memberOptions = useMemo(
    () =>
      (members.data ?? []).map((member) => ({
        value: member.userId,
        label: member.username,
        hint: member.email,
      })),
    [members.data],
  );

  const createdCount = generate.data?.createdEntryIds.length ?? 0;

  function setTimepointOffsetAt(index: number, value: string) {
    setTimepointOffsets((current) => {
      const next = timepoints.map((_, i) => current[i] ?? '');
      next[index] = value;
      return next;
    });
  }

  function setTreatmentOffsetAt(index: number, value: string) {
    setTreatmentOffsets((current) => current.map((v, i) => (i === index ? value : v)));
  }

  function addTreatmentOffset() {
    setTreatmentOffsets((current) => [...current, '']);
  }

  function removeTreatmentOffset(index: number) {
    setTreatmentOffsets((current) =>
      current.length === 1 ? [''] : current.filter((_, i) => i !== index),
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!batch.experimentalModelId) {
      toast('error', 'Vincule um modelo experimental à leva antes de gerar o cronograma.');
      return;
    }
    if (responsibles.length === 0) {
      toast('error', 'Escolha ao menos um responsável para o rodízio.');
      return;
    }

    // Mirror the backend rule: exactly one offset per model timepoint (avoids a 422 round-trip).
    const filledTimepointOffsets = renderedTimepointOffsets.filter((v) => v.trim() !== '');
    if (filledTimepointOffsets.length !== timepoints.length) {
      toast(
        'error',
        `Informe um dia para cada timepoint do modelo (${timepoints.length} no total).`,
      );
      return;
    }

    const treatmentDayOffsets = treatmentOffsets
      .map((v) => v.trim())
      .filter((v) => v !== '')
      .map(Number);

    try {
      await generate.mutateAsync({
        experimentalModelId: batch.experimentalModelId,
        startDate,
        treatmentDayOffsets,
        timepointDayOffsets: renderedTimepointOffsets.map(Number),
        responsibles,
        daysPerShift: Number(daysPerShift),
        reminderMinutesBefore: reminderEnabled ? Number(reminderMinutes) : undefined,
      });
      // Success stays on the modal so the operator sees the count + the calendar shortcut.
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível gerar o cronograma.');
    }
  }

  // Once the schedule is generated, swap the form for a success panel with the count and calendar shortcut.
  if (generate.isSuccess) {
    return (
      <Modal
        open
        onClose={onClose}
        title="Cronograma gerado"
        description={`${batch.name} · ${model.data?.name ?? 'modelo experimental'}`}
        footer={
          <Button onClick={onClose}>Fechar</Button>
        }
      >
        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-lg border bg-muted/30 p-4">
            <CalendarClock className="mt-0.5 size-5 text-primary" />
            <div>
              <p className="text-sm font-medium">
                {createdCount} entrada(s) criada(s) no calendário.
              </p>
              <p className="text-xs text-muted-foreground">
                Indução, tratamentos e timepoints, com o rodízio de responsáveis aplicado.
              </p>
            </div>
          </div>
          <Button asChild variant="outline" className="w-full">
            <Link to={`/agenda/schedule?experimentId=${experimentId}`}>
              <CalendarClock className="size-4" />
              Ver no calendário da Agenda
            </Link>
          </Button>
        </div>
      </Modal>
    );
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Gerar cronograma"
      description={`${batch.name} · ${model.data?.name ?? 'modelo experimental'}. A cadência de indução vem do modelo.`}
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={generate.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            form="generate-schedule-form"
            disabled={generate.isPending || model.isLoading}
          >
            {generate.isPending && <Loader2 className="size-4 animate-spin" />}
            Gerar cronograma
          </Button>
        </>
      }
    >
      {model.isLoading ? (
        <div className="flex items-center gap-2 p-6 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando modelo experimental…
        </div>
      ) : !batch.experimentalModelId || !model.data ? (
        <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
          A leva não tem um modelo experimental vinculado. Vincule um modelo para gerar o cronograma.
        </p>
      ) : (
        <form id="generate-schedule-form" className="space-y-6" onSubmit={handleSubmit} noValidate>
          {/* Induction cadence (read-only) — comes from the model, shown so the operator understands the base. */}
          <div className="rounded-lg border bg-muted/30 p-3 text-xs text-muted-foreground">
            Indução do modelo: {model.data.induction.administrations} administração(ões)
            {model.data.induction.administrations > 1
              ? `, a cada ${model.data.induction.intervalDays} dia(s)`
              : ''}
            . Dia de referência: {model.data.induction.referenceDayAfterInduction} após a indução.
          </div>

          {/* Start date (day 0 = first induction). */}
          <div className="space-y-1.5">
            <Label htmlFor="schedule-start">Data de início (1ª indução)</Label>
            <Input
              id="schedule-start"
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              required
            />
          </div>

          {/* Treatment day offsets — an editable list of day numbers relative to the start. */}
          <div className="space-y-2">
            <Label>Dias de tratamento (offset a partir do início)</Label>
            <div className="space-y-2">
              {treatmentOffsets.map((offset, index) => (
                <div key={index} className="flex items-center gap-2">
                  <Input
                    type="number"
                    min={0}
                    step={1}
                    value={offset}
                    onChange={(e) => setTreatmentOffsetAt(index, e.target.value)}
                    placeholder="Ex.: 7"
                    className="w-32"
                  />
                  <span className="text-xs text-muted-foreground">
                    {offset.trim() !== '' ? `dia +${offset}` : 'dia —'}
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="ml-auto"
                    onClick={() => removeTreatmentOffset(index)}
                    aria-label="Remover dia de tratamento"
                  >
                    <X className="size-4" />
                  </Button>
                </div>
              ))}
            </div>
            <Button type="button" variant="outline" size="sm" onClick={addTreatmentOffset}>
              <Plus className="size-4" />
              Adicionar dia
            </Button>
            <p className="text-xs text-muted-foreground">
              Deixe vazio se não houver dias de tratamento. Valores em branco são ignorados.
            </p>
          </div>

          {/* One day offset per model timepoint, in the model's order (mirrors the backend length check). */}
          <div className="space-y-2">
            <Label>Dia de cada timepoint do modelo</Label>
            {timepoints.length === 0 ? (
              <p className="rounded-md border border-dashed p-3 text-center text-xs text-muted-foreground">
                O modelo não define timepoints.
              </p>
            ) : (
              <div className="space-y-2">
                {timepoints.map((label, index) => (
                  <div key={`${label}-${index}`} className="flex items-center gap-3">
                    <Badge variant="muted" className="min-w-24 justify-center">
                      {label}
                    </Badge>
                    <Input
                      type="number"
                      min={0}
                      step={1}
                      value={renderedTimepointOffsets[index]}
                      onChange={(e) => setTimepointOffsetAt(index, e.target.value)}
                      placeholder="Dia (offset)"
                      className="w-32"
                      required
                    />
                    <span className="text-xs text-muted-foreground">
                      {renderedTimepointOffsets[index]?.trim()
                        ? `dia +${renderedTimepointOffsets[index]}`
                        : 'dia —'}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Roster of responsibles + rotation cadence. */}
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Responsáveis (rodízio)</Label>
              {members.isLoading ? (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Loader2 className="size-4 animate-spin" />
                  Carregando membros…
                </div>
              ) : (
                <MultiSelect
                  label="Responsáveis"
                  placeholder="Selecionar membros…"
                  options={memberOptions}
                  selected={responsibles}
                  onChange={setResponsibles}
                  className="w-full"
                />
              )}
              <p className="text-xs text-muted-foreground">
                A ordem de seleção define a alternância.
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="schedule-shift">Dias por turno</Label>
              <Input
                id="schedule-shift"
                type="number"
                min={1}
                step={1}
                value={daysPerShift}
                onChange={(e) => setDaysPerShift(e.target.value)}
                required
              />
              <p className="text-xs text-muted-foreground">
                Ex.: 2 responsáveis × 1 dia/turno = dia sim, dia não.
              </p>
            </div>
          </div>

          {/* Véspera reminder (default 1 day before). */}
          <div className="space-y-2 rounded-lg border p-3">
            <label className="flex items-center gap-2 text-sm font-medium">
              <input
                type="checkbox"
                checked={reminderEnabled}
                onChange={(e) => setReminderEnabled(e.target.checked)}
                className="size-4 rounded border-input"
              />
              Lembrete de véspera
            </label>
            {reminderEnabled && (
              <div className="flex items-center gap-2">
                <Input
                  type="number"
                  min={1}
                  step={1}
                  value={reminderMinutes}
                  onChange={(e) => setReminderMinutes(e.target.value)}
                  className="w-32"
                  required
                />
                <span className="text-xs text-muted-foreground">
                  minutos antes ({DEFAULT_REMINDER_MINUTES} = 1 dia)
                </span>
              </div>
            )}
          </div>
        </form>
      )}
    </Modal>
  );
}
