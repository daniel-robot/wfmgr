import { Injectable, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';

export type NotificationLevel = 'success' | 'info' | 'warning' | 'error';

export interface Notification {
  readonly id: number;
  readonly level: NotificationLevel;
  readonly title: string;
  readonly detail?: string;
  /** Optional pre-formatted lines (e.g. validation errors). */
  readonly lines?: readonly string[];
  /** Auto-dismiss timeout in ms; 0 = sticky. */
  readonly timeoutMs: number;
}

export interface NotifyOptions {
  detail?: string;
  lines?: readonly string[];
  timeoutMs?: number;
}

const DEFAULT_TIMEOUTS: Record<NotificationLevel, number> = {
  success: 4000,
  info: 5000,
  warning: 7000,
  error: 0, // sticky until dismissed
};

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly _items = signal<readonly Notification[]>([]);
  private nextId = 1;
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();

  readonly items = this._items.asReadonly();

  success(title: string, options?: NotifyOptions): number {
    return this.push('success', title, options);
  }

  info(title: string, options?: NotifyOptions): number {
    return this.push('info', title, options);
  }

  warning(title: string, options?: NotifyOptions): number {
    return this.push('warning', title, options);
  }

  error(title: string, options?: NotifyOptions): number {
    return this.push('error', title, options);
  }

  /**
   * Convenience: turn an HTTP error into a detailed notification.
   * Recognises:
   *   - { error: "..." }          (this API's current shape)
   *   - { message: "..." }
   *   - ProblemDetails { title, detail, status, errors? }
   *   - ModelState { errors: { field: ["..."] } }
   */
  fromHttpError(fallbackTitle: string, err: unknown): number {
    const parsed = parseHttpError(err);
    return this.error(parsed.title || fallbackTitle, {
      detail: parsed.detail,
      lines: parsed.lines,
    });
  }

  dismiss(id: number): void {
    const t = this.timers.get(id);
    if (t) {
      clearTimeout(t);
      this.timers.delete(id);
    }
    this._items.update((list) => list.filter((n) => n.id !== id));
  }

  clear(): void {
    for (const t of this.timers.values()) clearTimeout(t);
    this.timers.clear();
    this._items.set([]);
  }

  private push(level: NotificationLevel, title: string, options?: NotifyOptions): number {
    const id = this.nextId++;
    const timeoutMs = options?.timeoutMs ?? DEFAULT_TIMEOUTS[level];
    const note: Notification = {
      id,
      level,
      title,
      detail: options?.detail,
      lines: options?.lines,
      timeoutMs,
    };
    this._items.update((list) => [...list, note]);
    if (timeoutMs > 0) {
      const handle = setTimeout(() => this.dismiss(id), timeoutMs);
      this.timers.set(id, handle);
    }
    return id;
  }
}

interface ParsedError {
  title: string;
  detail?: string;
  lines?: string[];
}

function parseHttpError(err: unknown): ParsedError {
  if (!(err instanceof HttpErrorResponse)) {
    if (err instanceof Error) return { title: err.message };
    return { title: 'Request failed.' };
  }

  const status = err.status;
  const body = err.error;

  // Network / CORS / offline
  if (status === 0) {
    return { title: 'Network error', detail: 'Could not reach the server. Check your connection.' };
  }

  let title: string | undefined;
  let detail: string | undefined;
  const lines: string[] = [];

  if (typeof body === 'string' && body.trim().length > 0) {
    title = body;
  } else if (body && typeof body === 'object') {
    const b = body as Record<string, unknown>;
    title = pickString(b['title'], b['error'], b['message']);
    detail = pickString(b['detail'], b['description']);

    // ASP.NET ValidationProblemDetails: { errors: { field: ["msg", ...] } }
    const errs = b['errors'];
    if (errs && typeof errs === 'object') {
      for (const [field, msgs] of Object.entries(errs as Record<string, unknown>)) {
        if (Array.isArray(msgs)) {
          for (const m of msgs) {
            if (typeof m === 'string') {
              lines.push(field && field !== '$' ? `${field}: ${m}` : m);
            }
          }
        } else if (typeof msgs === 'string') {
          lines.push(field && field !== '$' ? `${field}: ${msgs}` : msgs);
        }
      }
    }
  }

  if (!title) {
    title = `HTTP ${status} ${err.statusText || ''}`.trim();
  }

  return { title, detail, lines: lines.length ? lines : undefined };
}

function pickString(...candidates: unknown[]): string | undefined {
  for (const c of candidates) {
    if (typeof c === 'string' && c.trim().length > 0) return c;
  }
  return undefined;
}
