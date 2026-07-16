import { Link } from 'react-router-dom';
import { Boxes, QrCode } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { useAuth } from '@/modules/auth/AuthProvider';
import { greetingFor } from '@/modules/dashboard/components/dashboard-presentation';

/**
 * Dashboard greeting header: a time-of-day salutation with the signed-in user's name and a primary
 * action into the stock screen. Presentational — reads the session from the auth context and renders;
 * no data fetching of its own.
 */
export function DashboardHeader() {
  const { user } = useAuth();
  const greeting = greetingFor(new Date().getHours());
  const name = user?.username ?? '';

  return (
    <div className="flex flex-col gap-3 border-b pb-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {greeting}
          {name ? `, ${name}` : ''}
        </h1>
        <p className="text-sm text-muted-foreground">
          Resumo do estoque, do consumo e dos alertas ativos do laboratório.
        </p>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline">
          <Link to="/quick-consumption">
            <QrCode />
            Registro rápido
          </Link>
        </Button>
        <Button asChild>
          <Link to="/inventory">
            <Boxes />
            Ir para o estoque
          </Link>
        </Button>
      </div>
    </div>
  );
}
