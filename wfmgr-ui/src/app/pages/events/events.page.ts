import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-events-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './events.page.html',
  styleUrl: './events.page.css'
})
export class EventsPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(WorkflowApiService);

  busy = false;
  error = '';
  ok = '';

  readonly ctForm = this.fb.group({
    externalEventId: [crypto.randomUUID(), Validators.required],
    accessionNumber: ['', Validators.required],
    studyInstanceUid: ['', Validators.required],
    seriesInstanceUids: ['', Validators.required],
    modality: ['CT', Validators.required],
    wadoRsUrl: ['', Validators.required],
    occurredAt: [new Date().toISOString(), Validators.required]
  });

  readonly pvmedForm = this.fb.group({
    externalEventId: [crypto.randomUUID(), Validators.required],
    caseId: ['', Validators.required],
    type: ['PROGRESS', Validators.required],
    jobId: ['', Validators.required],
    status: ['Running', Validators.required],
    progress: [50],
    studyInstanceUid: [''],
    seriesInstanceUid: [''],
    occurredAt: [new Date().toISOString(), Validators.required]
  });

  sendCt(): void {
    if (this.ctForm.invalid) {
      this.ctForm.markAllAsTouched();
      return;
    }

    this.run(() =>
      this.api.simulateCtImageStored({
        externalEventId: this.ctForm.value.externalEventId!,
        accessionNumber: this.ctForm.value.accessionNumber!,
        dicomRef: {
          studyInstanceUid: this.ctForm.value.studyInstanceUid!,
          seriesInstanceUids: this.ctForm.value.seriesInstanceUids!.split(',').map((x) => x.trim()),
          modality: this.ctForm.value.modality!
        },
        dicomWebLocation: {
          wadoRsUrl: this.ctForm.value.wadoRsUrl!
        },
        occurredAt: this.ctForm.value.occurredAt!
      })
    );
  }

  sendPvMed(type?: string): void {
    if (this.pvmedForm.invalid) {
      this.pvmedForm.markAllAsTouched();
      return;
    }

    const actualType = type ?? this.pvmedForm.value.type!;

    this.run(() =>
      this.api.simulatePvMedEvent({
        externalEventId: this.pvmedForm.value.externalEventId!,
        caseId: this.pvmedForm.value.caseId!,
        type: actualType,
        pvMedJob: {
          jobId: this.pvmedForm.value.jobId!,
          status: this.pvmedForm.value.status!,
          progress: this.pvmedForm.value.progress
        },
        pvMedResult:
          actualType === 'PVMED_AUTOCONTOUR_COMPLETED'
            ? {
                rtStructLocation: {
                  studyInstanceUid: this.pvmedForm.value.studyInstanceUid || '',
                  seriesInstanceUid: this.pvmedForm.value.seriesInstanceUid || ''
                }
              }
            : null,
        occurredAt: this.pvmedForm.value.occurredAt!
      })
    );
  }

  private run(work: () => import('rxjs').Observable<unknown>): void {
    this.busy = true;
    this.error = '';
    this.ok = '';

    work()
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: () => {
          this.ok = 'Event submitted.';
          this.ctForm.patchValue({ externalEventId: crypto.randomUUID() });
          this.pvmedForm.patchValue({ externalEventId: crypto.randomUUID() });
        },
        error: (err) => (this.error = err?.error?.message ?? 'Event submit failed.')
      });
  }
}
