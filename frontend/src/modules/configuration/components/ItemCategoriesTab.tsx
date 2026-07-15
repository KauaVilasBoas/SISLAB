import { useState, type FormEvent } from 'react';
import { Loader2, Plus, Tags } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Modal } from '@/shared/components/ui/modal';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import {
  useCreateItemCategory,
  useItemCategories,
} from '@/modules/configuration/api/configuration.queries';
import {
  CatalogueEmpty,
  CatalogueError,
  CatalogueLoading,
} from '@/modules/configuration/components/CatalogueState';

/** "Item categories" tab: lists per-tenant categories and creates new ones. */
export function ItemCategoriesTab() {
  const categories = useItemCategories();
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-end">
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="size-4" />
          Nova categoria
        </Button>
      </div>

      {categories.isLoading ? (
        <CatalogueLoading label="Carregando categorias…" />
      ) : categories.isError ? (
        <CatalogueError label="Não foi possível carregar as categorias." />
      ) : categories.data && categories.data.length > 0 ? (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="px-5 py-3 font-medium">Nome</th>
                  <th className="px-5 py-3 font-medium">Sinônimos</th>
                  <th className="px-5 py-3 font-medium">Tipo</th>
                </tr>
              </thead>
              <tbody>
                {categories.data.map((category) => (
                  <tr key={category.id} className="border-b last:border-0">
                    <td className="px-5 py-3 font-medium">{category.name}</td>
                    <td className="px-5 py-3 text-muted-foreground">
                      {category.aliases || '—'}
                    </td>
                    <td className="px-5 py-3">
                      {category.isControlled ? (
                        <Badge variant="secondary">Controlado</Badge>
                      ) : (
                        <span className="text-muted-foreground">Comum</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      ) : (
        <CatalogueEmpty
          icon={<Tags className="size-8" />}
          message="Nenhuma categoria cadastrada. Crie a primeira categoria de itens."
        />
      )}

      {createOpen ? <CreateItemCategoryModal onClose={() => setCreateOpen(false)} /> : null}
    </div>
  );
}

function CreateItemCategoryModal({ onClose }: { onClose: () => void }) {
  const create = useCreateItemCategory();
  const toast = useToast();
  const [name, setName] = useState('');
  const [aliases, setAliases] = useState('');
  const [isControlled, setIsControlled] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsedAliases = aliases
      .split(',')
      .map((alias) => alias.trim())
      .filter((alias) => alias.length > 0);
    try {
      await create.mutateAsync({
        name: name.trim(),
        aliases: parsedAliases.length > 0 ? parsedAliases : null,
        isControlled,
      });
      toast('success', 'Categoria criada com sucesso.');
      onClose();
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível criar a categoria.');
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Nova categoria"
      description="Classifique os itens do estoque; marque como controlado quando exigir rastreio especial."
      footer={
        <>
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>
            Cancelar
          </Button>
          <Button type="submit" form="create-category-form" disabled={create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar categoria
          </Button>
        </>
      }
    >
      <form
        id="create-category-form"
        className="flex flex-col gap-4"
        onSubmit={handleSubmit}
        noValidate
      >
        <div className="flex flex-col gap-2">
          <Label htmlFor="category-name">Nome</Label>
          <Input
            id="category-name"
            placeholder="Reagente"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="category-aliases">Sinônimos (opcional)</Label>
          <Input
            id="category-aliases"
            placeholder="Separe por vírgula: solvente, diluente"
            value={aliases}
            onChange={(e) => setAliases(e.target.value)}
          />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 rounded border-input"
            checked={isControlled}
            onChange={(e) => setIsControlled(e.target.checked)}
          />
          Item controlado
        </label>
      </form>
    </Modal>
  );
}
