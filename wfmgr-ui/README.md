# wfmgr-ui

Angular 21 testing console for the radiotherapy workflow backend.

## Purpose

This app is intentionally simple and built for backend workflow testing, not production UX. It lets developers exercise the full case lifecycle without needing Postman or curl.

Supported operations:
- Create a case
- Submit simulation record
- Simulate CT image stored event
- Simulate PvMed autocontour event callback
- View all cases
- View case details, work items, and audit timeline
- Manually trigger forward to Monaco

---

## Stack

- Angular 21 (standalone components, zoneful change detection)
- TypeScript 5.9
- Angular Router, HttpClient, Reactive Forms
- zone.js (required for HTTP change detection)

---

## Configuration — Backend Base URL

The API base URL is set in environment files:

| File | Used when |
|------|-----------|
| `src/environments/environment.development.ts` | `npm start` / `ng serve` |
| `src/environments/environment.ts` | `ng build` (production) |

**To change the API port or host**, edit `src/environments/environment.development.ts`:

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5223'  // ← change this
};
```

The default `http://localhost:5223` matches the backend's `launchSettings.json` development profile.

---

## Run

```bash
npm install
npm start
```

Open: **`http://localhost:4200`**

---

## Build

```bash
npm run build
```

Output goes to `dist/wfmgr-ui/`.

---

## Routes

| Route | Page |
|-------|------|
| `/` | Dashboard |
| `/cases` | Case list |
| `/cases/new` | Create case |
| `/cases/:caseId` | Case details — actions, work items, audit timeline |
| `/events` | Standalone event simulator |
| `/audit-logs` | Global audit log |
| `/monaco-forward` | Manual Monaco forward test |

---

## Backend API Endpoints Used

| Method | Endpoint |
|--------|----------|
| `POST` | `/api/cases` |
| `GET`  | `/api/cases` |
| `GET`  | `/api/cases/{caseId}` |
| `GET`  | `/api/cases/{caseId}/work-items` |
| `GET`  | `/api/cases/{caseId}/audit-logs` |
| `POST` | `/api/cases/{caseId}/sim-record` |
| `POST` | `/api/cases/{caseId}/forward/monaco` |
| `POST` | `/api/integration/ct/image-stored` |
| `POST` | `/api/integration/pvmed/events` |
| `GET`  | `/api/audit-logs` |

---

## Notes

- CORS must be enabled on the backend for the Angular dev server origin (`http://localhost:4200`). This is the default in `appsettings.Development.json`.
- zone.js is included as a dependency and loaded via `angular.json` polyfills. It is required for `HttpClient` subscription callbacks to trigger change detection.

