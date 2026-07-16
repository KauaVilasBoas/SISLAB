import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ApiError } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import { Field, Select } from '@/modules/inventory/components/form-controls';
import {
  useRegisterPartner,
  useUpdatePartner,
} from '@/modules/inventory/api/partner.queries';
import {
  PARTNER_TYPES,
  partnerTypePresentation,
} from '@/modules/inventory/components/partner-presentation';
import type {
  PartnerDetail,
  PartnerType,
  RegisterPartnerRequest,
  UpdatePartnerRequest,
} from '@/modules/inventory/partner.types';

interface PartnerFormModalProps {
  /** When provided, the modal edits this partner; otherwise it registers a new one. */
  partner?: PartnerDetail;
  onClose: () => void;
}

interface FormState {
  name: string;
  type: PartnerType;
  document: string;
  contactEmail: string;
  description: string;
}

function initialState(partner?: PartnerDetail): FormState {
  return {
    name: partner?.name ?? '',
    type: partner?.type ?? 'Supplier',
    document: partner?.cnpj ?? '',
    contactEmail: partner?.email ?? '',
    description: partner?.notes ?? '',
  };
}

/** Turns a blank string into null; used for the optional fields the backend accepts. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/**
 * Create/edit-a-partner form (card [E7] #48). Both variants post/put the same descriptive shape (name,
 * role, document, contact e-mail and a free-text description of what the partner supplies/does). On
 * success it invalidates the partner namespace and closes the modal.
 */
export function PartnerFormModal({ partner, onClose }: PartnerFormModalProps) {
  const isEdit = Boolean(partner);
  const toast = useToast();
  const register = useRegisterPartner();
  const update = useUpdatePartner(partner?.id ?? '');

  const [form, setForm] = useState<FormState>(() => initialState(partner));

  const pending = register.isPending || update.isPending;

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const body: RegisterPartnerRequest & UpdatePartnerRequest = {
      name: form.name.trim(),
      type: form.type,
      document: orNull(form.document),
      contactEmail: orNull(form.contactEmail),
      description: orNull(form.description),
    };

    try {
      if (isEdit) {
        await update.mutateAsync(body);
        toast('success', 'Parceiro atualizado.');
      } else {
        await register.mutateAsync(body);
        toast('success', 'Parceiro cadastrado.');
      }
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível salvar o parceiro.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={isEdit ? 'Editar parceiro' : 'Novo parceiro'}
      description={
        isEdit
          ? 'Atualize os dados do parceiro.'
          : 'Cadastre uma instituição fornecedora ou parceira do laboratório.'
      }
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={pending}>
            Cancelar
          </Button>
          <Button type="submit" form="partner-form" disabled={pending}>
            {pending && <Loader2 className="size-4 animate-spin" />}
            {isEdit ? 'Salvar alterações' : 'Cadastrar parceiro'}
          </Button>
        </>
      }
    >
      <form
        id="partner-form"
        className="grid grid-cols-1 gap-4 sm:grid-cols-2"
        onSubmit={handleSubmit}
        noValidate
      >
        <Field label="Nome" htmlFor="partner-name" className="sm:col-span-2">
          <Input
            id="partner-name"
            value={form.name}
            onChange={(e) => patch('name', e.target.value)}
            placeholder="Ex.: Sigma-Aldrich"
            required
            autoFocus
          />
        </Field>

        <Field label="Tipo" htmlFor="partner-type">
          <Select
            id="partner-type"
            value={form.type}
            onChange={(e) => patch('type', e.target.value as PartnerType)}
          >
            {PARTNER_TYPES.map((type) => (
              <option key={type} value={type}>
                {partnerTypePresentation(type).label}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="CNPJ (opcional)" htmlFor="partner-document">
          <Input
            id="partner-document"
            value={form.document}
            onChange={(e) => patch('document', e.target.value)}
            placeholder="00.000.000/0000-00"
          />
        </Field>

        <Field label="E-mail de contato (opcional)" htmlFor="partner-email">
          <Input
            id="partner-email"
            type="email"
            value={form.contactEmail}
            onChange={(e) => patch('contactEmail', e.target.value)}
            placeholder="contato@parceiro.com"
          />
        </Field>

        <div className="hidden sm:block" aria-hidden />

        <Field
          label="Descrição (opcional)"
          htmlFor="partner-description"
          className="sm:col-span-2"
        >
          <Input
            id="partner-description"
            value={form.description}
            onChange={(e) => patch('description', e.target.value)}
            placeholder="Ex.: Fornece reagentes e envia amostras (compostos GDA) para teste."
          />
        </Field>
      </form>
    </Modal>
  );
}
