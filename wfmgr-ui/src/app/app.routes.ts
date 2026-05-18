import { Routes } from '@angular/router';
import { AuditLogPageComponent } from './pages/audit/audit-log.page';
import { CaseDetailsPageComponent } from './pages/cases/case-details.page';
import { DashboardPageComponent } from './pages/dashboard/dashboard.page';
import { EventsPageComponent } from './pages/events/events.page';
import { MonacoForwardPageComponent } from './pages/monaco/monaco-forward.page';
import { WorkflowConfigPageComponent } from './pages/workflow-config/workflow-config.page';
import { LoginPageComponent } from './pages/login/login.page';
import { authGuard, adminGuard } from './core/services/auth.guard';

export const routes: Routes = [
	{ path: 'login', component: LoginPageComponent },

	{ path: '', component: DashboardPageComponent, canActivate: [authGuard] },

	// Redirect-only aliases kept for backward compatibility.
	{ path: 'patients', redirectTo: '', pathMatch: 'full' },
	{ path: 'cases', redirectTo: '', pathMatch: 'full' },
	{ path: 'cases/new', redirectTo: '', pathMatch: 'full' },

	{ path: 'cases/:caseId', component: CaseDetailsPageComponent, canActivate: [authGuard] },
	{ path: 'events', component: EventsPageComponent, canActivate: [authGuard] },
	{ path: 'workflow-config', component: WorkflowConfigPageComponent, canActivate: [authGuard, adminGuard] },
	{ path: 'audit-logs', component: AuditLogPageComponent, canActivate: [authGuard] },
	{ path: 'monaco-forward', component: MonacoForwardPageComponent, canActivate: [authGuard] },

	{ path: '**', redirectTo: '' }
];
