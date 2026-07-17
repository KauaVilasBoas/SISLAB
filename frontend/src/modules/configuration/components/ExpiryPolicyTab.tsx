import { useEffect, useState, type FormEvent } from 'react';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import {
  useExpiryPolicy,
  useSetExpiryPolicy,
} from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/**
 * "Expiry policy" tab: a single form to set the warning window (days before expiry) that drives the
 * "expiring soon" alerts. Unlike the other tabs there is no list/modal — the policy is a single
 * value per company, so it edits in place, seeded from the current GET value.
 */
export function ExpiryPolicyTab() {
  const policy = useExpiryPolicy();
  const save = useSetExpiryPolicy();
  const toast = useToast();
  const [days, setDays] = useState('');

  // Seed the input once the current window loads (and whenever the server value changes).
  useEffect(() => {
    if (policy.data !== undefined) setDays(String(policy.data));
  }, [policy.data]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsed = Number(days.trim());
    if (!Number.isInteger(parsed) || parsed < 0) {
      toast('error', 'Informe um número inteiro de dias maior ou igual a zero.');
      return;
    }
    try {
      await save.mutateAsync({ warningWindowDays: parsed });
      toast('success', 'Política de validade atualizada com sucesso.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível salvar a política de validade.');
    }
  }

  if (policy.isLoading) return <CatalogueLoading label="Carregando política de validade…" />;
  if (policy.isError) return <CatalogueError label="Não foi possível carregar a política de validade." />;

  return (
    <Card>
      <CardContent className="p-5">
        <form className="flex max-w-md flex-col gap-4" onSubmit={handleSubmit} noValidate>
          <div className="flex flex-col gap-2">
            <Label htmlFor="expiry-window">Janela de alerta (dias antes do vencimento)</Label>
            <Input
              id="expiry-window"
              type="number"
              min={0}
              step={1}
              inputMode="numeric"
              value={days}
              onChange={(e) => setDays(e.target.value)}
              required
            />
            <p className="text-xs text-muted-foreground">
              Itens serão sinalizados como "vencendo em breve" quando faltarem até esta quantidade de
              dias para o vencimento.
            </p>
          </div>
          <div className="flex justify-end">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              <Save className="size-4" />
              Salvar política
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
