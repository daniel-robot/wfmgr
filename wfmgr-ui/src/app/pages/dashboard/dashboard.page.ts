import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.css'
})
export class DashboardPageComponent {
  readonly cards = [
    { title: 'Cases', description: 'Browse and inspect workflow cases.', route: '/cases' },
    { title: 'Events', description: 'Send CT and PvMed simulation events.', route: '/events' },
    { title: 'Audit Logs', description: 'Review processing timeline entries.', route: '/audit-logs' },
    { title: 'Monaco Forward Test', description: 'Trigger manual Monaco forwarding.', route: '/monaco-forward' }
  ];
}
