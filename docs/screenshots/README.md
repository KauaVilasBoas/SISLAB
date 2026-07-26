# Screenshots

Shot list for the images referenced by the root [README](../../README.md). Filenames are fixed —
the README already points at them, so dropping a file in with the right name is all that's needed.

## Before capturing

1. Start the API and the SPA:
   ```powershell
   dotnet run --project src/Host/SISLAB.Api
   cd frontend; npm run dev
   ```
2. Log in with the dev seed admin and activate the LAFTE company.
3. Make sure the data on screen looks like a working lab, not an empty state — a few stock items
   with real names, at least one project with groups and animals, one plate, a couple of bookings.
   An empty table is worse than no screenshot.
4. Capture at **1440×900 or wider**, browser zoom at 100 %, no devtools panel, no browser chrome if
   possible (macOS `⌘⇧4`, Windows `Win+Shift+S`).
5. Prefer PNG. Keep each file under ~500 KB so the README stays fast to load.

> Sensitive data: the pilot lab is a real client. Before committing, check that no real person's
> email, real patient/animal identifiers, or unpublished compound names are visible. Rename to
> plausible fakes in the seed if needed.

## Required shots

| File | Screen | What must be visible |
|---|---|---|
| `dashboard.png` | Dashboard | The ECharts widgets with actual series — consumption over time, stock levels, expiry/low-stock counters. This is the hero image at the top of the README, so it carries the most weight. |
| `in-vivo-project.png` | In vivo → Project detail | The `Project → Batch → Group → Animal` hierarchy with real group labels (Naive, Control, dose groups), animal weights, and the generated schedule with rotating responsibles. This is the shot that shows the domain depth. |
| `dilution-calculator.png` | Experiments → Dilution calculator | A filled-in serial dilution: stock solution inputs, the dilution factor, and the resulting concentration points with per-step volumes. Ideally with the diluent volume clearly shown — it's the calculation the README opens with. |
| `inventory.png` | Inventory | The stock item list with batches, quantities, storage locations, and at least one expiry or below-minimum badge lit up. |
| `agenda.png` | Agenda → Calendar | The calendar with several bookings across rooms, plus a presentation or bioterium assignment so it's clearly more than a room booker. |

## Optional extras

Worth adding if they look good — the README can grow a row for them:

- `labels.png` — the QR/barcode label generator with a printable sheet rendered.
- `collection-plan.png` — the collection sheet with roles assigned and the sample → analysis →
  storage matrix.
- `audit.png` — the audit trail list, showing actor, action, entity and timestamp.
- `plate.png` — an in vitro plate layout with typed well roles and the control wells marked.
- `login.png` — the login screen, for the visual identity / branding.

## After capturing

Only the root README needs updating: remove the `> [!NOTE]` block in its **Screenshots** section
once the five required files are in place.
