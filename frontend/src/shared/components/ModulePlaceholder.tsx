import { Construction } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Card, CardContent } from '@/shared/components/ui/card';

interface ModulePlaceholderProps {
  title: string;
  description?: string;
}

/**
 * Temporary content for module mother screens that aren't implemented yet.
 * Keeps routing/navigation complete while features are built incrementally.
 */
export function ModulePlaceholder({ title, description }: ModulePlaceholderProps) {
  return (
    <div className="space-y-6">
      <PageHeader title={title} description={description} />
      <Card>
        <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
          <Construction className="size-8 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Tela em construção. A arquitetura está pronta — os componentes deste
            módulo serão adicionados aqui.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
