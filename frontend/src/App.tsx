import { RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/app/providers';
import { router } from '@/app/router';
import { IS_DEMO } from '@/demo/isDemo';
import { DemoRibbon } from '@/demo/DemoRibbon';

export default function App() {
  return (
    <AppProviders>
      <RouterProvider router={router} />
      {IS_DEMO && <DemoRibbon />}
    </AppProviders>
  );
}
