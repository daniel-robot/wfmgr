import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NotificationService } from '../services/notification.service';

@Component({
  selector: 'app-notification-host',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="notif-host" aria-live="polite" aria-atomic="false">
      @for (n of notifications(); track n.id) {
        <div class="notif notif-{{ n.level }}" role="status">
          <div class="notif-icon" aria-hidden="true">
            @switch (n.level) {
              @case ('success') { ✓ }
              @case ('info') { i }
              @case ('warning') { ! }
              @case ('error') { ✕ }
            }
          </div>
          <div class="notif-body">
            <div class="notif-title">{{ n.title }}</div>
            @if (n.detail) {
              <div class="notif-detail">{{ n.detail }}</div>
            }
            @if (n.lines?.length) {
              <ul class="notif-lines">
                @for (line of n.lines; track line) {
                  <li>{{ line }}</li>
                }
              </ul>
            }
          </div>
          <button
            type="button"
            class="notif-close"
            aria-label="Dismiss notification"
            (click)="notify.dismiss(n.id)"
          >
            ×
          </button>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .notif-host {
        position: fixed;
        top: 1rem;
        right: 1rem;
        z-index: 10000;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        max-width: min(420px, calc(100vw - 2rem));
        pointer-events: none;
      }
      .notif {
        pointer-events: auto;
        display: grid;
        grid-template-columns: 1.5rem 1fr auto;
        gap: 0.6rem;
        align-items: start;
        padding: 0.75rem 0.85rem;
        border-radius: 6px;
        border: 1px solid rgba(0, 0, 0, 0.08);
        box-shadow: 0 6px 16px rgba(0, 0, 0, 0.18);
        background: #fff;
        color: #1f2937;
        font-size: 0.9rem;
        line-height: 1.35;
        animation: notif-in 0.18s ease-out;
      }
      .notif-icon {
        width: 1.5rem;
        height: 1.5rem;
        border-radius: 50%;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        font-weight: 700;
        font-size: 0.9rem;
        color: #fff;
      }
      .notif-success { border-left: 4px solid #2e7d32; }
      .notif-success .notif-icon { background: #2e7d32; }
      .notif-info    { border-left: 4px solid #1976d2; }
      .notif-info    .notif-icon { background: #1976d2; }
      .notif-warning { border-left: 4px solid #ed6c02; }
      .notif-warning .notif-icon { background: #ed6c02; }
      .notif-error   { border-left: 4px solid #c62828; }
      .notif-error   .notif-icon { background: #c62828; }

      .notif-title {
        font-weight: 600;
        word-break: break-word;
      }
      .notif-detail {
        margin-top: 0.2rem;
        color: #374151;
        white-space: pre-wrap;
        word-break: break-word;
      }
      .notif-lines {
        margin: 0.35rem 0 0;
        padding-left: 1.1rem;
        color: #374151;
      }
      .notif-lines li { margin: 0.15rem 0; }
      .notif-close {
        appearance: none;
        background: transparent;
        border: 0;
        color: #6b7280;
        font-size: 1.15rem;
        line-height: 1;
        cursor: pointer;
        padding: 0 0.2rem;
      }
      .notif-close:hover { color: #111827; }

      @keyframes notif-in {
        from { opacity: 0; transform: translateY(-6px); }
        to   { opacity: 1; transform: translateY(0); }
      }
    `,
  ],
})
export class NotificationHostComponent {
  protected readonly notify = inject(NotificationService);
  protected readonly notifications = this.notify.items;
}
