import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  Input,
  ViewChild,
  forwardRef,
  inject
} from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface ChipOption {
  code: string;
  description?: string | null;
  group?: string | null;
}

/**
 * Reusable vocabulary-backed multi-select rendered as removable chips with a
 * typeahead input. Implements ControlValueAccessor so it can be used with both
 * template-driven and reactive forms (`formControlName="..."`). The value is a
 * `string[]` of selected codes.
 *
 * `strict` mode (default) only allows values that exist in `options` — used for
 * gate checks and case statuses where the server rejects unknown codes.
 * Non-strict mode allows free entry; unknown values are surfaced with a
 * warning style so authors notice they're outside the vocabulary.
 */
@Component({
  selector: 'app-chip-multi-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ChipMultiSelectComponent),
      multi: true
    }
  ],
  template: `
    <div class="cms" [class.disabled]="isDisabled" (click)="focusInput()">
      <span class="cms-chip"
            *ngFor="let v of selected; trackBy: trackByValue"
            [class.unknown]="isUnknown(v)"
            [title]="describe(v) || (isUnknown(v) ? 'Not in vocabulary' : v)">
        <span class="cms-chip-label">{{ v }}</span>
        <button type="button"
                class="cms-chip-x"
                aria-label="Remove"
                [disabled]="isDisabled"
                (click)="remove(v, $event)">×</button>
      </span>
      <input #input
             type="text"
             class="cms-input"
             [placeholder]="selected.length === 0 ? placeholder : ''"
             [(ngModel)]="query"
             (ngModelChange)="onQueryChange()"
             (keydown)="onKeydown($event)"
             (focus)="open()"
             [disabled]="isDisabled" />
    </div>
    <div class="cms-menu" *ngIf="menuOpen && filteredOptions.length > 0">
      <button type="button"
              class="cms-menu-item"
              *ngFor="let o of filteredOptions; let i = index; trackBy: trackByOption"
              [class.active]="i === activeIndex"
              (mousedown)="selectOption(o, $event)"
              (mouseenter)="activeIndex = i">
        <span class="cms-menu-code">{{ o.code }}</span>
        <span class="cms-menu-desc" *ngIf="o.description">{{ o.description }}</span>
        <span class="cms-menu-group" *ngIf="o.group">{{ o.group }}</span>
      </button>
    </div>
    <div class="cms-menu cms-menu-empty"
         *ngIf="menuOpen && filteredOptions.length === 0 && query.trim().length > 0 && !strict">
      <button type="button"
              class="cms-menu-item add-custom"
              (mousedown)="addCustom($event)">
        + Add "<strong>{{ query.trim() }}</strong>" (not in vocabulary)
      </button>
    </div>
  `,
  styleUrl: './chip-multi-select.component.css'
})
export class ChipMultiSelectComponent implements ControlValueAccessor {
  @Input() options: ChipOption[] = [];
  @Input() placeholder = 'Type to search…';
  /** When true, only options from `options` may be selected. */
  @Input() strict = true;

  @ViewChild('input') inputRef?: ElementRef<HTMLInputElement>;

  selected: string[] = [];
  query = '';
  menuOpen = false;
  activeIndex = 0;
  isDisabled = false;

  private readonly cdr = inject(ChangeDetectorRef);
  private onChange: (value: string[]) => void = () => {};
  private onTouched: () => void = () => {};

  // ── CVA ────────────────────────────────────────────────────────────────
  writeValue(value: string[] | null | undefined): void {
    this.selected = Array.isArray(value) ? [...value] : [];
    this.cdr.markForCheck();
  }
  registerOnChange(fn: (value: string[]) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
    this.cdr.markForCheck();
  }

  // ── Interaction ────────────────────────────────────────────────────────
  get filteredOptions(): ChipOption[] {
    const q = this.query.trim().toLowerCase();
    const taken = new Set(this.selected);
    return this.options
      .filter((o) => !taken.has(o.code))
      .filter((o) =>
        q.length === 0
          ? true
          : o.code.toLowerCase().includes(q) ||
            (o.description ?? '').toLowerCase().includes(q)
      )
      .slice(0, 50);
  }

  isUnknown(code: string): boolean {
    return !this.options.some((o) => o.code === code);
  }

  describe(code: string): string | null {
    return this.options.find((o) => o.code === code)?.description ?? null;
  }

  trackByValue(_: number, v: string) {
    return v;
  }
  trackByOption(_: number, o: ChipOption) {
    return o.code;
  }

  focusInput(): void {
    this.inputRef?.nativeElement.focus();
  }

  open(): void {
    this.menuOpen = true;
    this.activeIndex = 0;
  }

  close(): void {
    this.menuOpen = false;
  }

  onQueryChange(): void {
    this.open();
    this.activeIndex = 0;
  }

  onKeydown(ev: KeyboardEvent): void {
    if (ev.key === 'ArrowDown') {
      ev.preventDefault();
      this.open();
      this.activeIndex = Math.min(this.activeIndex + 1, this.filteredOptions.length - 1);
    } else if (ev.key === 'ArrowUp') {
      ev.preventDefault();
      this.activeIndex = Math.max(this.activeIndex - 1, 0);
    } else if (ev.key === 'Enter') {
      ev.preventDefault();
      const opt = this.filteredOptions[this.activeIndex];
      if (opt) {
        this.selectOption(opt);
      } else if (!this.strict && this.query.trim().length > 0) {
        this.addCustom();
      }
    } else if (ev.key === 'Backspace' && this.query.length === 0 && this.selected.length > 0) {
      this.remove(this.selected[this.selected.length - 1]);
    } else if (ev.key === 'Escape') {
      this.close();
    } else if ((ev.key === ',' || ev.key === 'Tab') && this.query.trim().length > 0) {
      // Comma / Tab also commit the current value (paste-friendly).
      const opt = this.filteredOptions[0];
      if (opt) {
        ev.preventDefault();
        this.selectOption(opt);
      } else if (!this.strict) {
        ev.preventDefault();
        this.addCustom();
      }
    }
  }

  selectOption(o: ChipOption, ev?: Event): void {
    ev?.preventDefault();
    if (this.isDisabled) return;
    if (!this.selected.includes(o.code)) {
      this.selected = [...this.selected, o.code];
      this.emit();
    }
    this.query = '';
    this.activeIndex = 0;
    this.focusInput();
  }

  addCustom(ev?: Event): void {
    ev?.preventDefault();
    if (this.isDisabled) return;
    const v = this.query.trim();
    if (v.length === 0 || this.selected.includes(v)) return;
    this.selected = [...this.selected, v];
    this.query = '';
    this.emit();
    this.focusInput();
  }

  remove(code: string, ev?: Event): void {
    ev?.preventDefault();
    if (this.isDisabled) return;
    this.selected = this.selected.filter((s) => s !== code);
    this.emit();
  }

  @HostListener('document:click', ['$event'])
  onDocClick(ev: MouseEvent): void {
    const host = (this.inputRef?.nativeElement.closest('app-chip-multi-select')) as HTMLElement | null;
    if (host && !host.contains(ev.target as Node)) {
      this.close();
      this.onTouched();
    }
  }

  private emit(): void {
    this.onChange(this.selected);
  }
}
