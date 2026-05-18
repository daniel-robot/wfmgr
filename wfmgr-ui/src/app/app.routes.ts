import { Routes } from '@angular/router';
import { AuditLogPageComponent } from './pages/audit/audit-log.page';
import { CaseDetailsPageComponent } from './pages/cases/case-details.page';
import { DashboardPageComponent } from './pages/dashboard/dashboard.page';
import { EventsPageComponent } from './pages/events/events.page';
import { MonacoForwardPageComponent } from './pages/monaco/monaco-forward.page';
import { WorkflowConfigPageComponent } from './pages/workflow-config/workflow-config.page';
import { WorkflowTransitionsPageComponent } from './pages/workflow-transitions/workflow-transitions.page';
import { WorkflowVocabularyPageComponent } from './pages/workflow-vocabulary/workflow-vocabulary.page';
import { CaseStatusOverlaysPageComponent } from './pages/case-status-overlays/case-status-overlays.page';
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
	{ path: 'workflow-transitions', component: WorkflowTransitionsPageComponent, canActivate: [authGuard, adminGuard] },
	{ path: 'workflow-vocabulary', component: WorkflowVocabularyPageComponent, canActivate: [authGuard, adminGuard] },
	{ path: 'case-status-overlays', component: CaseStatusOverlaysPageComponent, canActivate: [authGuard, adminGuard] },
	{ path: 'audit-logs', component: AuditLogPageComponent, canActivate: [authGuard] },
	{ path: 'monaco-forward', component: MonacoForwardPageComponent, canActivate: [authGuard] },

	{ path: '**', redirectTo: '' }
];
