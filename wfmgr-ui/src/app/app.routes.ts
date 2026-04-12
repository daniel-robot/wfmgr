import { Routes } from '@angular/router';
import { AuditLogPageComponent } from './pages/audit/audit-log.page';
import { CaseDetailsPageComponent } from './pages/cases/case-details.page';
import { CaseListPageComponent } from './pages/cases/case-list.page';
import { CreateCasePageComponent } from './pages/cases/create-case.page';
import { DashboardPageComponent } from './pages/dashboard/dashboard.page';
import { EventsPageComponent } from './pages/events/events.page';
import { MonacoForwardPageComponent } from './pages/monaco/monaco-forward.page';
import { PatientListPageComponent } from './pages/patients/patient-list.page';

export const routes: Routes = [
	{ path: '', component: DashboardPageComponent },
	{ path: 'patients', component: PatientListPageComponent },
	{ path: 'cases', redirectTo: 'patients', pathMatch: 'full' },
	{ path: 'cases/new', redirectTo: 'patients', pathMatch: 'full' },
	{ path: 'cases/:caseId', component: CaseDetailsPageComponent },
	{ path: 'events', component: EventsPageComponent },
	{ path: 'audit-logs', component: AuditLogPageComponent },
	{ path: 'monaco-forward', component: MonacoForwardPageComponent },
	{ path: '**', redirectTo: '' }
];
