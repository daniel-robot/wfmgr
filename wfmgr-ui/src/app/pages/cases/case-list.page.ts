import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CaseSummary } from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-case-list-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './case-list.page.html',
  styleUrl: './case-list.page.css'
})
export class CaseListPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly router = inject(Router);

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
        },
        error: (err) => (this.error = err?.error?.message ?? 'Failed to load cases.')
      });
  }

  getStatusClass(status: string): string {
    return `status-${status.toLowerCase()}`;
  }

  openCase(caseId: string): void {
    this.router.navigate(['/cases', caseId]);
  }
}
