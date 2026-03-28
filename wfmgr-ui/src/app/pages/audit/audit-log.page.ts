import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { finalize } from 'rxjs';
import { AuditLogItem } from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-audit-log-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './audit-log.page.html',
  styleUrl: './audit-log.page.css'
})
export class AuditLogPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);

  logs: AuditLogItem[] = [];
  loading = false;
  error = '';

  ngOnInit(): void {
    this.loading = true;
    this.error = '';

    this.api
      .getAuditLogs()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => (this.logs = data),
        error: (err) => (this.error = err?.error?.message ?? 'Failed to load audit logs.')
      });
  }
}
