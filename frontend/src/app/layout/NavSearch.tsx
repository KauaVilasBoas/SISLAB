import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
} from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { Lock, Search, Sparkles } from 'lucide-react';
import { navGroups, type NavItem } from '@/app/navigation';
import { usePermissions } from '@/modules/auth/PermissionsProvider';
import { matchesAllTerms, toSearchTerms } from '@/shared/lib/search';
import { cn } from '@/shared/lib/utils';

interface ResultGroup {
  title: string;
  items: NavItem[];
}

function isPaletteShortcut(event: KeyboardEvent): boolean {
  return (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
}

export function NavSearch() {
  const [open, setOpen] = useState(false);
  const openerRef = useRef<HTMLButtonElement | null>(null);

  const openPalette = useCallback((opener?: HTMLButtonElement | null) => {
    openerRef.current = opener ?? null;
    setOpen(true);
  }, []);

  const closePalette = useCallback(() => {
    setOpen(false);
    openerRef.current?.focus();
  }, []);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (!isPaletteShortcut(event)) return;
      event.preventDefault();
      setOpen((current) => !current);
    }

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  return (
    <>
      <button
        type="button"
        onClick={(event) => openPalette(event.currentTarget)}
        className="mr-1 hidden h-9 w-64 items-center gap-2 rounded-md border border-input bg-background px-3 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground lg:flex"
      >
        <Search className="size-4" aria-hidden />
        <span className="flex-1 text-left">Buscar telas…</span>
        <kbd className="pointer-events-none rounded border bg-muted px-1.5 font-mono text-[10px] font-medium">
          ⌘K
        </kbd>
      </button>

      <button
        type="button"
        onClick={(event) => openPalette(event.currentTarget)}
        aria-label="Buscar telas"
        className="rounded-md p-2 text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground lg:hidden"
      >
        <Search className="size-4" aria-hidden />
      </button>

      {open && <NavSearchDialog onClose={closePalette} />}
    </>
  );
}

function NavSearchDialog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const { hasAnyPermission, isReady } = usePermissions();
  const baseId = useId();
  const listRef = useRef<HTMLDivElement>(null);

  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);

  const groups = useMemo<ResultGroup[]>(() => {
    const terms = toSearchTerms(query);

    const canSee = (item: NavItem) =>
      !item.permissionAny || (isReady && hasAnyPermission(item.permissionAny));

    return navGroups
      .map((group) => ({
        title: group.title,
        items: group.items.filter(
          (item) =>
            canSee(item) &&
            !item.disabled &&
            matchesAllTerms(`${item.label} ${item.description}`, terms),
        ),
      }))
      .filter((group) => group.items.length > 0);
  }, [query, hasAnyPermission, isReady]);

  const flatItems = useMemo(() => groups.flatMap((group) => group.items), [groups]);

  useEffect(() => {
    setActiveIndex(0);
  }, [query]);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, []);

  useEffect(() => {
    listRef.current
      ?.querySelector('[data-active="true"]')
      ?.scrollIntoView({ block: 'nearest' });
  }, [activeIndex]);

  const optionId = (index: number) => `${baseId}-option-${index}`;

  function select(item: NavItem | undefined) {
    if (!item) return;
    navigate(item.path);
    onClose();
  }

  function handleKeyDown(event: ReactKeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      select(flatItems[activeIndex]);
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (flatItems.length === 0) return;
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActiveIndex((current) => (current + step + flatItems.length) % flatItems.length);
      return;
    }
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      setActiveIndex(event.key === 'Home' ? 0 : flatItems.length - 1);
    }
  }

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex justify-center bg-black/50 p-4 pt-[10vh]"
      onMouseDown={onClose}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Buscar telas"
        className="flex h-fit max-h-[70vh] w-full max-w-xl flex-col overflow-hidden rounded-xl border bg-popover text-popover-foreground shadow-2xl"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="flex items-center gap-3 border-b px-4">
          <Search className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          <input
            autoFocus
            type="text"
            role="combobox"
            aria-expanded
            aria-controls={`${baseId}-listbox`}
            aria-activedescendant={
              flatItems.length > 0 ? optionId(activeIndex) : undefined
            }
            aria-autocomplete="list"
            aria-label="Buscar telas"
            placeholder="Buscar telas… (estoque, controlados, diluição)"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={handleKeyDown}
            className="h-12 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          />
          <kbd className="pointer-events-none hidden rounded border bg-muted px-1.5 py-0.5 font-mono text-[10px] font-medium text-muted-foreground sm:block">
            esc
          </kbd>
        </div>

        <div
          ref={listRef}
          id={`${baseId}-listbox`}
          role="listbox"
          aria-label="Telas encontradas"
          className="flex-1 overflow-y-auto p-2"
        >
          {flatItems.length === 0 ? (
            <p className="px-3 py-8 text-center text-sm text-muted-foreground">
              Nada encontrado para “{query.trim()}”.
            </p>
          ) : (
            groups.map((group) => {
              const headingId = `${baseId}-group-${group.title}`;
              return (
                <div key={group.title} role="group" aria-labelledby={headingId}>
                  <p
                    id={headingId}
                    className="px-3 pb-1 pt-3 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground first:pt-1"
                  >
                    {group.title}
                  </p>
                  {group.items.map((item) => {
                    const index = flatItems.indexOf(item);
                    return (
                      <NavSearchOption
                        key={item.path}
                        id={optionId(index)}
                        item={item}
                        active={index === activeIndex}
                        onHover={() => setActiveIndex(index)}
                        onSelect={() => select(item)}
                      />
                    );
                  })}
                </div>
              );
            })
          )}
        </div>

        <div className="hidden items-center gap-4 border-t px-4 py-2 text-[11px] text-muted-foreground sm:flex">
          <span>
            <kbd className="font-mono">↑</kbd> <kbd className="font-mono">↓</kbd> navegar
          </span>
          <span>
            <kbd className="font-mono">↵</kbd> abrir
          </span>
          <span>
            <kbd className="font-mono">esc</kbd> fechar
          </span>
        </div>
      </div>
    </div>,
    document.body,
  );
}

interface NavSearchOptionProps {
  id: string;
  item: NavItem;
  active: boolean;
  onHover: () => void;
  onSelect: () => void;
}

function NavSearchOption({ id, item, active, onHover, onSelect }: NavSearchOptionProps) {
  const Icon = item.icon;

  return (
    <div
      id={id}
      role="option"
      aria-selected={active}
      data-active={active}
      onMouseMove={onHover}
      onClick={onSelect}
      className={cn(
        'flex cursor-pointer items-center gap-3 rounded-md px-3 py-2',
        active && 'bg-accent text-accent-foreground',
      )}
    >
      <Icon
        className={cn(
          'size-4 shrink-0',
          item.premium ? 'text-premium' : 'text-muted-foreground',
        )}
        aria-hidden
      />
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-1.5">
          <span className="truncate text-sm font-medium">{item.label}</span>
          {item.premium && (
            <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-premium/15 px-1.5 py-px text-[9px] font-semibold uppercase tracking-wide text-premium">
              <Sparkles className="size-2.5" aria-hidden />
              Premium
            </span>
          )}
        </span>
        <span className="block truncate text-[11px] text-muted-foreground">
          {item.description}
        </span>
      </span>
      {item.premium && <Lock className="size-3.5 shrink-0 text-premium/80" aria-hidden />}
    </div>
  );
}
