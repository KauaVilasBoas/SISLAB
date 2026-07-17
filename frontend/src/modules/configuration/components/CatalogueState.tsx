import type { ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';

/** Uniform loading placeholder shared by every configuration tab. */
export function CatalogueLoading({ label }: { label: string }) {
  return (
    <Card>
      <CardContent className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        {label}
      </CardContent>
    </Card>
  );
}

/** Uniform error placeholder shared by every configuration tab. */
export function CatalogueError({ label }: { label: string }) {
  return (
    <Card>
      <CardContent className="py-16 text-center text-sm text-destructive">{label}</CardContent>
    </Card>
  );
}

/** Uniform empty-state placeholder shared by every configuration tab. */
export function CatalogueEmpty({ icon, message }: { icon: ReactNode; message: string }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
        <span className="text-muted-foreground">{icon}</span>
        <p className="text-sm text-muted-foreground">{message}</p>
      </CardContent>
    </Card>
  );
}
