import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CaseSummary } from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

type WorkflowStepState = 'completed' | 'current' | 'upcoming';

interface WorkflowStep {
  id: string;
  label: string;
}

@Component({
  selector: 'app-case-list-page',
  imports: [CommonModule, RouterLink],
  templateUrl: './case-list.page.html',
  styleUrl: './case-list.page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseListPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly workflowSteps: WorkflowStep[] = [
    { id: 'intake', label: 'Intake' },
    { id: 'simulation', label: 'Simulation' },
    { id: 'contouring', label: 'Contouring' },
    { id: 'planning', label: 'Planning' },
    { id: 'treatment', label: 'Treatment' }
  ];

  private readonly statusToStep = new Map<string, number>([
    ['Draft', 0],
    ['Submitted', 0],
    ['Cancelled', 0],
    ['SimScheduled', 1],
    ['SimInProgress', 1],
    ['SimCompleted', 1],
    ['ImageStored', 1],
    ['ImageForwarding', 1],
    ['ContouringInProgress', 2],
    ['ContoursReady', 2],
    ['ContoursUnderReview', 2],
    ['ContoursRejected', 2],
    ['ContourReworkRequired', 2],
    ['PlanningPending', 3],
    ['PlanningAssigned', 3],
    ['PlanningInProgress', 3],
    ['PlanReady', 3],
    ['PlanUnderReview', 3],
    ['PlanReviewed', 3],
    ['PlanReReviewOptional', 3],
    ['PrescriptionGenerating', 3],
    ['PrescriptionReady', 3],
    ['PrescriptionSyncFailed', 3],
    ['PlanQAInProgress', 3],
    ['PlanQAApproved', 3],
    ['PlanQAFailed', 3],
    ['PlanDoubleCheckOptional', 3],
    ['ReadyForScheduling', 4],
    ['SchedulingInProgress', 4],
    ['SchedulingFailed', 4],
    ['Scheduled', 4],
    ['OrderPending', 4],
    ['OrderSubmitted', 4],
    ['QueuePending', 4],
    ['Treating', 4],
    ['TreatmentPaused', 4],
    ['TreatmentInterrupted', 4],
    ['Completed', 4],
    ['MonacoForwarded', 4]
  ]);

  cases: CaseSummary[] = [];
  loading = false;
  error = '';
  info = '';

  ngOnInit(): void {
    this.loadCases();
  }

  loadCases(): void {
    this.loading = true;
    this.error = '';
    this.info = '';

    this.api
      .getCases()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => {
          this.cases = data;
          this.info = `Loaded ${data.length} case(s).`;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error = err?.error?.message ?? 'Failed to load cases.';
          this.cdr.markForCheck();
        }
      });
  }

  getStatusClass(status: string): string {
    return `status-${status.toLowerCase()}`;
  }

  getWorkflowStepState(status: string, stepIndex: number): WorkflowStepState {
    const currentStep = this.getCurrentWorkflowStep(status);
    if (stepIndex < currentStep) {
      return 'completed';
    }

    if (stepIndex === currentStep) {
      return 'current';
    }

    return 'upcoming';
  }

  private getCurrentWorkflowStep(status: string): number {
    return this.statusToStep.get(status) ?? 0;
  }

  openCase(caseId: string): void {
    this.router.navigate(['/cases', caseId]);
  }
}
